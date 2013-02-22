using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using Common;
using SDMX.Parsers;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Collections.ObjectModel;
using OXM;

namespace SDMX
{
    public abstract partial class DataReader : IDisposable, IEnumerable<KeyValuePair<string, object>>
    {
        const string _IsValidColumnName = "IsValid";
        const string _ErrorStringColumnName = "ErrorMessages";
        /// <summary>
        /// The key family used by the current reader
        /// </summary>
        public KeyFamily KeyFamily { get; private set; }

        /// <summary>
        /// Throw an exception if the reader encounters an error; otherwise, don't throw an exception
        /// and add the error the the Errors property and set the IsValid to false.
        /// The default is true.
        /// </summary>
        public bool ThrowExceptionIfNotValid { get; set; }


        public bool DetectDuplicateKeys { get; set; }

        /// <summary>
        /// The inner xml reader used by the reader.
        /// </summary>
        public XmlReader XmlReader { get; private set; }

        /// <summary>
        /// The line number at the current reader position.
        /// </summary>
        public int LineNumber
        {
            get
            {
                return ((IXmlLineInfo)XmlReader).LineNumber;
            }
        }

        /// <summary>
        /// The line position at the current reader position.
        /// </summary>
        public int LinePosition
        {
            get
            {
                return ((IXmlLineInfo)XmlReader).LinePosition;
            }
        }

        public ReadOnlyCollection<Error> Errors
        {
            get
            {
                return _errors.AsReadOnly();
            }
        }

        public string ErrorString { get; protected set; }

        public bool IsValid
        {
            get
            {
                return _errors.Count == 0;
            }
        }

        List<Error> _errors = new List<Error>();
        List<Error> _seriesErrors = new List<Error>();
        List<Error> _obsErrors = new List<Error>();
        Dictionary<string, KeyValuePair<string, object>> _groupValues = new Dictionary<string, KeyValuePair<string, object>>();
        Dictionary<string, Dictionary<string, KeyValuePair<string, object>>> _groups = new Dictionary<string, Dictionary<string, KeyValuePair<string, object>>>();
        Dictionary<string, object> _record = new Dictionary<string, object>();
        Dictionary<string, KeyValuePair<string, object>> _tempRecord = new Dictionary<string, KeyValuePair<string, object>>();
        Dictionary<string, KeyValuePair<string, object>> _seriesValues = new Dictionary<string, KeyValuePair<string, object>>();
        Dictionary<string, KeyValuePair<string, object>> _obsValues = new Dictionary<string, KeyValuePair<string, object>>();

        Dictionary<string, Dictionary<string, Component>> _groupComponents = new Dictionary<string, Dictionary<string, Component>>();
        Dictionary<string, Component> _seriesComponenets = null;
        Dictionary<string, Component> _obsComponenets = null;
        List<Attribute> _optionalAttributes = null;
        Dictionary<string, string> _keys = new Dictionary<string, string>();
        DataMapper _mapper = new DataMapper();

        DataTable _table = null;

        internal DataReader(XmlReader reader, KeyFamily keyFamily)
        {
            XmlReader = reader;
            KeyFamily = keyFamily;
            ThrowExceptionIfNotValid = true;
        }

        /// <summary>
        /// Allows to map the reader field names to different ones.
        /// </summary>        
        /// <param name="source">The name of the source field.</param>
        /// /// <param name="source">The name of the destination field.</param>
        /// <param name="castAction">The cast action.</param>
        public void Map(string source, string destination)
        {
            _mapper.Map(source, destination);
        }

        /// <summary>
        /// Allows to map the reader field names to different ones and set the cast action.
        /// </summary>        
        /// <param name="source">The name of the source field.</param>
        /// /// <param name="source">The name of the destination field.</param>
        /// <param name="castAction">The cast action.</param>
        public void Map(string source, string destination, Func<object, object> castAction)
        {
            _mapper.Map(source, destination, castAction);
        }

        protected void ClearObs()
        {
            _obsValues.Clear();
            _obsErrors.Clear();
        }

        protected void ClearSeries()
        {
            _seriesValues.Clear();
            _seriesErrors.Clear();
        }

        protected void NewGroupValues()
        {
            _groupValues = new Dictionary<string, KeyValuePair<string, object>>();
        }

        protected void SetGroup(Group group, string name, string value)
        {
            if (_groupValues.ContainsKey(name))
            {
                AddValidationError(false, "Duplicate '{0}' in the same group.");
                return;
            }

            var comp = FindGroupComponent(group, name);
                        
            object obj;
            if (TryParse(false, name, value, comp, out obj))
            {
                _groupValues.Add(name, KeyVal(value, obj));
            }
            else
            {
                _groupValues.Add(name, KeyVal(value, null));
            }
        }

        KeyValuePair<string, object> KeyVal(string value, object obj)
        {
            return new KeyValuePair<string, object>(value, obj);
        }

        protected void SetSeries(string name, string value)
        {
            if (_seriesValues.ContainsKey(name))
            {
                AddValidationError(true, "Duplicate '{0}' in the same series.", name);
                return;
            }

            var comp = FindSeriesComponent(name);

            object obj;
            if (TryParse(true, name, value, comp, out obj))
            {
                _seriesValues.Add(name, KeyVal(value, obj));
            }
            else
            {
                _seriesValues.Add(name, KeyVal(value, null));
            }
        }

        protected void SetObs(string name, string value)
        {
            if (_seriesValues.ContainsKey(name))
            {
                AddValidationError(false, "Duplicate '{0}' in the series of the observation.", name);
                return;
            }

            if (_obsValues.ContainsKey(name))
            {
                AddValidationError(false, "Duplicate '{0}' in the same observation.", name);
                return;
            }

            var comp = FindObsComponent(name);

            object obj;
            if (TryParse(false, name, value, comp, out obj))
            {
                _obsValues.Add(name, KeyVal(value, obj));
            }
            else
            {
                _obsValues.Add(name, KeyVal(value, null));
            }
        }

        protected void ValidateGroup(Group group)
        {
            bool valid = ValidateDimensions(group.Dimensions, _groupValues, true, true);
            ValidateAttributes(GroupAttributes(group), _groupValues, true);

            if (!valid)
                return;

            string key = BuildGroupKey(group, _groupValues);

            Dictionary<string, KeyValuePair<string, object>> existing = null;
            if (!_groups.TryGetValue(key, out existing))
            {
                _groups.Add(key, _groupValues);
            }
            else
            {
                if (!IsDictEqual(existing, _groupValues))
                {
                    AddValidationError(false, "2 Occurances for group (id={0}) that have the same key but differnt values. Value1: ({1}) Value2: ({2}).",
                        group.Id, RecordToString(existing), RecordToString(_groupValues));
                }
            }
        }

        protected void ValidateSeries()
        {
            ValidateDimensions(KeyFamily.Dimensions, _seriesValues, true, true);
            ValidateAttributes(SeriesAttributes(), _seriesValues, true);
        }


        protected void ValidateObs()
        {
            if (KeyFamily.TimeDimension != null)
            {
                ValidateDimension(_obsValues, KeyFamily.TimeDimension.Concept.Id, true, false);
            }

            if (KeyFamily.PrimaryMeasure != null)
            {
                ValidateDimension(_obsValues, KeyFamily.PrimaryMeasure.Concept.Id, true, false);
            }

            ValidateAttributes(ObsAttributes(), _obsValues, false);
        }

        private bool TryParse(bool isSeries, string name, string value, Component comp, out object obj)
        {
            obj = null;
            if (comp == null)
            {
                AddValidationError(isSeries, "Invalid tag '{0}'.", name);
                return false;
            }

            string startTime = null;
            if (!comp.TryParse(value, startTime, out obj))
            {
                AddParseError(string.Format("Cannot parse value '{1}' for '{0}'.", name, value), isSeries, name, value);
                return false;
            }

            return true ;
        }
        bool ValidateDimensions(IEnumerable<Dimension> dimensions, Dictionary<string, KeyValuePair<string, object>> values, bool logErrors, bool isSeries)
        {
            bool valid = true;            
            foreach (var id in dimensions.Where(d => d != null).Select(d => d.Concept.Id))
            {
                if (!ValidateDimension(values, id, logErrors, isSeries))
                {
                    valid = false;
                }
            }

            return valid;
        }

        bool ValidateDimension(Dictionary<string, KeyValuePair<string, object>> values, string id, bool logErrors, bool isSeries)
        {
            if (!values.ContainsKey(id))
            {
                if (logErrors)
                {
                    AddMandatoryComponentMissingError(string.Format("Value for dimension '{0}' is missing.", id), isSeries, id);
                }
                
                return false;
            }

            return true;
        }

        void ValidateAttributes(IEnumerable<Attribute> attributes, Dictionary<string, KeyValuePair<string, object>> values, bool isSeries)
        {
            foreach (var attr in attributes)
            {
                string id = attr.Concept.Id;
                if (!values.ContainsKey(id))
                {
                    if (attr.AssignmentStatus != AssignmentStatus.Conditional)
                    {
                        AddMandatoryComponentMissingError(string.Format("Value for mandatory attribute '{0}' is missing.", id), isSeries, id);
                    }
                }
            }
        }

        protected void SetRecord()
        {
            _record.Clear();
            _tempRecord.Clear();            

            // add values for series, obs and each group
            _seriesValues.ForEach(i => _tempRecord.Add(i.Key, i.Value));
            _obsValues.ForEach(i => _tempRecord.Add(i.Key, i.Value));

            if (DetectDuplicateKeys)
            {
                string key = BuildKey(_tempRecord);
                if (key != null)
                {
                    if (_keys.ContainsKey(key))
                    {
                        AddDuplicateKeyError(string.Format("Duplicate key found: {0}", key), false, key);
                    }
                    else
                    {
                        _keys.Add(key, null);
                    }
                }
            }

            foreach (var group in KeyFamily.Groups)
            {
                bool valid = ValidateDimensions(group.Dimensions, _tempRecord, false, false);

                if (!valid)
                {
                    continue;
                }

                string groupKey = BuildGroupKey(group, _tempRecord);

                Dictionary<string, KeyValuePair<string, object>> groupValues;
                if (_groups.TryGetValue(groupKey, out groupValues))
                {
                    foreach (var groupValue in groupValues)
                    {
                        if (!_tempRecord.ContainsKey(groupValue.Key))
                        {
                            _tempRecord.Add(groupValue.Key, groupValue.Value);
                        }
                    }
                }
            }

            // Fill optional attributes
            foreach (var attr in GetOptionalAttributes())
            {
                string name = attr.Concept.Id;
                if (!_tempRecord.ContainsKey(name))
                {
                    _tempRecord[name] = new KeyValuePair<string, object>(null, null);
                }
            }

            _errors.Clear();
            foreach (var error in _seriesErrors)
            {
                _errors.Add(error);
            }
            foreach (var error in _obsErrors)
            {
                _errors.Add(error);
            }

            ErrorString = GetErrorString(_errors);

            _mapper.MapRecord(_record, _tempRecord);

            _record[_ErrorStringColumnName] = ErrorString;
            _record[_IsValidColumnName] = IsValid;
        }

        DataTable GetTable()
        {
            if (_table == null)
                BuildTable();

            return _table;
        }

        public object this[string name]
        {
            get
            {
                object value = null;
                if (!_record.TryGetValue(name, out value))
                {
                    throw new SDMXException("{0} not found.", name);
                }
                return value;
            }
        }

        /// <summary>
        /// Tries to get the value for the name if one exists.
        /// </summary>
        public bool TryGetValue(string name, out object value)
        {
            return _record.TryGetValue(name, out value);
        }

        /// <summary>
        /// Returns true if the record contains the name; otherwise false.
        /// </summary>
        public bool Contains(string name)
        {
            return _record.ContainsKey(name);
        }

        /// <summary>
        /// Create a data reader based on the type of the file. This mathod looks into the first element of the file
        /// and creates the right reader (Generic, Compact, Utility, CrossSectional).
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="keyFamily">The key family.</param>
        /// <returns>An instance of the DataReader.</returns>
        public static DataReader Create(string fileName, KeyFamily keyFamily)
        {
            Contract.AssertNotNullOrEmpty(fileName, "fileName");
            return Create(XmlReader.Create(fileName), keyFamily);
        }

        /// <summary>
        /// Create a data reader based on the type of the stream. This mathod looks into the first element of the stream
        /// and creates the right reader (Generic, Compact, Utility, CrossSectional).
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="keyFamily">The key family.</param>
        /// <returns>An instance of the DataReader.</returns>
        public static DataReader Create(Stream stream, KeyFamily keyFamily)
        {
            Contract.AssertNotNull(stream, "stream");
            return Create(XmlReader.Create(stream), keyFamily);
        }

        /// <summary>
        /// Create a data reader based on an xml data reader. This mathod looks into the first element
        /// and creates the right reader (Generic, Compact, Utility, CrossSectional).
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="keyFamily">The key family.</param>
        /// <returns>An instance of the DataReader.</returns>
        public static DataReader Create(XmlReader reader, KeyFamily keyFamily)
        {
            Contract.AssertNotNull(reader, "reader");

            if (reader.ReadState == ReadState.Initial)
            {
                reader.Read();
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.ReadNextStartElement();
            }

            if (reader.LocalName == "CompactData")
                return new CompactDataReader(reader, keyFamily);
            else if (reader.LocalName == "GenericData")
                return new GenericDataReader(reader, keyFamily);
            else
                throw new SDMXException("Unsupported root element ({0}) for data file.", reader.LocalName);
        }

        public abstract bool Read();

        public Header ReadHeader()
        {
            return ReadHeader(null);
        }

        /// <summary>
        /// Read the head of a Data Message. This should be done first before calling the Read method.
        /// </summary>
        /// <returns>The header instance.</returns>
        public Header ReadHeader(Action<ValidationMessage> validationAction)
        {
            CheckDisposed();

            while (XmlReader.Read() && !XmlReader.IsStartElement() && XmlReader.LocalName != "DataSet")
                continue;

            if (XmlReader.LocalName == "Header")
            {
                var map = new OXM.FragmentMap<Header>(Namespaces.Message + "Header", new HeaderMap());
                return map.ReadXml(XmlReader, validationAction);
            }

            return null;
        }

        protected void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("DataReader");
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (var item in _record)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        string BuildGroupKey(Group group, Dictionary<string, KeyValuePair<string, object>> values)
        {
            var groupKey = new Dictionary<string, KeyValuePair<string, object>>();
            foreach (var id in group.Dimensions.Where(d => d != null).Select(d => d.Concept.Id))
            {
                groupKey.Add(id, values[id]);
            }
            return string.Format("id={0},{1}", group.Id, RecordToString(groupKey));
        }

        string BuildKey(Dictionary<string, KeyValuePair<string, object>> values)
        {
            var key = new Dictionary<string, KeyValuePair<string, object>>();
            foreach (var id in KeyFamily.Dimensions.Select(d => d.Concept.Id))
            {
                KeyValuePair<string, object> value = new KeyValuePair<string, object>();
                if (values.TryGetValue(id, out value))
                {
                    key.Add(id, value);
                }
                else
                {
                    return null;
                }
            }
            string name = KeyFamily.TimeDimension.Concept.Id;
            key.Add(name, values[name]);
            return RecordToString(key);
        }

        string RecordToString(Dictionary<string, KeyValuePair<string, object>> record)
        {
            var list = new List<string>();
            record.ForEach(i => list.Add(string.Format("{0}={1}", i.Key, i.Value.Key)));
            return string.Join(",", list.ToArray());
        }

        void BuildTable()
        {
            Func<string, Type, bool, DataColumn> col = (name, type, isNull) =>
            {                
                KeyValuePair<string, object> value = new KeyValuePair<string, object>();
                if (_mapper.TryGetValue(name, out value))
                {
                    name = value.Key;
                    if (value.Value != null)
                    {
                        type = ((Delegate)value.Value).Method.ReturnType;
                    }
                }
                var c = new DataColumn(name, type);
                c.AllowDBNull = isNull;
                return c;
            };

            _table = new DataTable();
            _table.TableName = KeyFamily.Id;

            foreach (var dim in KeyFamily.Dimensions)
                _table.Columns.Add(col(dim.Concept.Id, dim.GetValueType(), false));

            _table.Columns.Add(
                col(KeyFamily.TimeDimension.Concept.Id, KeyFamily.TimeDimension.GetValueType(), false));

            _table.Columns.Add(
                col(KeyFamily.PrimaryMeasure.Concept.Id, KeyFamily.PrimaryMeasure.GetValueType(), false));

            foreach (var attr in KeyFamily.Attributes)
                _table.Columns.Add(col(attr.Concept.Id, attr.GetValueType(),
                    attr.AssignmentStatus == AssignmentStatus.Conditional));

            _table.Columns.Add(col(_IsValidColumnName, typeof(bool), false));
            _table.Columns.Add(col(_ErrorStringColumnName, typeof(string), true));
        }

        //object Cast(string source, string destination, object castAction, object oldValue)
        //{
        //    try
        //    {
        //        return ((Delegate)castAction).DynamicInvoke(oldValue);
        //    }
        //    catch (Exception ex)
        //    {
        //        if (ex is TargetInvocationException && ex.InnerException != null)
        //            ex = ex.InnerException;

        //        string message = string.Format("Exception at position ({5},{6}) occured in the cast action 'Map(\"{0}\",\"{1}\", castAction)' when casting value '{2}' of type '{3}' (see inner exception for details): {4}", source, destination, oldValue, oldValue.GetType(), ex.Message, LineNumber, LinePosition);
        //        throw new SDMXException(message, ex);
        //    }
        //}

        void AddError(Error error, bool isSeries)
        {
            if (ThrowExceptionIfNotValid)
            {
                throw SDMXValidationException.Create(new List<Error> { error } , error.Message);
            }
            else
            {
                if (isSeries)
                    _seriesErrors.Add(error);
                else
                    _obsErrors.Add(error);
            }
        }
     
        protected void AddValidationError(string message, bool isSeries)
        {
            AddError(new ValidationError(LineNumber, LinePosition, string.Format("Validation error at ({0},{1}): {2}", LineNumber, LinePosition, message)), isSeries);
        }

        protected void AddValidationError(bool isSeries, string format, params object[] args)
        {
            AddValidationError(string.Format(format, args), isSeries);
        }

        protected void AddParseError(string message, bool isSeries, string name, string value)
        {
            AddError(new ParseError(name, value, LineNumber, LinePosition, string.Format("Parse error at ({0},{1}): {2}", LineNumber, LinePosition, message)), isSeries);
        }

        protected void AddDuplicateKeyError(string message, bool isSeries, string key)
        {
            AddError(new DuplicateKeyError(key, LineNumber, LinePosition, string.Format("Duplicate key error at ({0},{1}): {2}", LineNumber, LinePosition, message)), isSeries);
        }

        protected void AddMandatoryComponentMissingError(string message, bool isSeries, string id)
        {
            AddError(new MandatoryComponentMissing(id, string.Format("Mandatory component missing error at ({0},{1}): {2}", LineNumber, LinePosition, message)), isSeries);
        }

        protected bool IsNullOrEmpty(string s)
        {
            return s == null || s.Trim() == "";
        }

        string GetErrorString(List<Error> errors)
        {
            if (errors.Count == 0) return null;

            var builder = new StringBuilder();
            errors.ForEach(i => builder.AppendLine(i.Message));

            return builder.ToString();
        }

        bool IsDictEqual(Dictionary<string, KeyValuePair<string, object>> dict1, Dictionary<string, KeyValuePair<string, object>> dict2)
        {
            if (dict1.Count != dict2.Count)
                return false;

            foreach (var item1 in dict1)
            {
                var value2 = new KeyValuePair<string, object>();
                if (!dict2.TryGetValue(item1.Key, out value2))
                    return false;

                if (!item1.Value.Equals(value2))
                    return false;
            }

            return true;
        }

        Component FindGroupComponent(Group group, string name)
        {
            Dictionary<string, Component> components = null;
            if (!_groupComponents.TryGetValue(group.Id, out components))
            {
                components = BuildGroupComponents(group);
                _groupComponents.Add(group.Id, components);
            }

            Component comp = null;
            components.TryGetValue(name, out comp);
            return comp;
        }

        Component FindSeriesComponent(string name)
        {
            if (_seriesComponenets == null)
            {
                _seriesComponenets = BuildSeriesComponents();
            }

            Component comp = null;
            _seriesComponenets.TryGetValue(name, out comp);
            return comp;
        }

        Component FindObsComponent(string name)
        {
            if (_obsComponenets == null)
            {
                _obsComponenets = BuildObsComponents();
            }

            Component comp = null;
            _obsComponenets.TryGetValue(name, out comp);
            return comp;
        }

        List<Attribute> GetOptionalAttributes()
        {
            if (_optionalAttributes == null)
            {
                _optionalAttributes = BuildOptionalAttributes();
            }
            return _optionalAttributes;
        }

        List<Attribute> BuildOptionalAttributes()
        {
            var list = new List<Attribute>();
            foreach (var attr in KeyFamily.Attributes.Where(a => a.AssignmentStatus == AssignmentStatus.Conditional))
            {
                list.Add(attr);
            }
            return list;
        }

        IEnumerable<Attribute> GroupAttributes(Group group)
        {
            return KeyFamily.Attributes.Where(i => i.AttachementLevel == AttachmentLevel.Group && i.AttachmentGroups.Contains(group.Id));
        }

        IEnumerable<Attribute> SeriesAttributes()
        {
            return KeyFamily.Attributes.Where(i => i.AttachementLevel == AttachmentLevel.Series);
        }

        IEnumerable<Attribute> ObsAttributes()
        {
            return KeyFamily.Attributes.Where(i => i.AttachementLevel == AttachmentLevel.Observation);
        }

        Dictionary<string, Component> BuildGroupComponents(Group group)
        {
            var result = new Dictionary<string, Component>();
            AddCompoenents(result, group.Dimensions);
            AddCompoenents(result, GroupAttributes(group));
            return result;
        }

        Dictionary<string, Component> BuildSeriesComponents()
        {
            var result = new Dictionary<string, Component>();
            AddCompoenents(result, KeyFamily.Dimensions);
            AddCompoenents(result, SeriesAttributes());
            return result;
        }

        Dictionary<string, Component> BuildObsComponents()
        {
            var result = new Dictionary<string, Component>();

            if (KeyFamily.TimeDimension != null)
            {
                result.Add(KeyFamily.TimeDimension.Concept.Id, KeyFamily.TimeDimension);
            }

            if (KeyFamily.PrimaryMeasure != null)
            {
                result.Add(KeyFamily.PrimaryMeasure.Concept.Id, KeyFamily.PrimaryMeasure);
            }

            AddCompoenents(result, ObsAttributes());

            return result;
        }

        private void AddCompoenents(Dictionary<string, Component> result, IEnumerable components)
        {
            foreach (Component comp in components)
            {
                result.Add(comp.Concept.Id, comp);
            }
        }

        #region IDisposable

        bool _disposed = false;

        /// <summary>
        /// Dispose the reader.
        /// </summary>        
        public void Dispose()
        {
            if (!_disposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        ~DataReader()
        {
            Dispose(false);
        }

        public void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_table != null) _table.Clear();


                _obsValues.Clear();
                _seriesValues.Clear();
                _groups.Clear();
                _record.Clear();
                _keys.Clear();
                ((IDisposable)XmlReader).Dispose();
            }

            _table = null;

            _disposed = true;
        }

        #endregion        
    }
}
