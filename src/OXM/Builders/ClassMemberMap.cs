using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Common;
using System.Runtime.Serialization;

namespace OXM
{
    public class AttributeGroupMemberMap<TObj, TProperty>
    {
        AttributeGroupTypeMap<TProperty> _groupTypeMap;
        MemberMap<TObj, TProperty> _memberMap;

        public AttributeGroupMemberMap(Expression<Func<TObj, TProperty>> property)
        {
            _memberMap = new MemberMap<TObj, TProperty>(property);
        }

        public AttributeGroupMemberMap<TObj, TProperty> Set(Action<TProperty> set)
        {
            _memberMap.Set(set);
            return this;
        }

        public void GroupTypeMap(AttributeGroupTypeMap<TProperty> groupTypeMap)
        {
            _groupTypeMap = groupTypeMap;
        }

        internal Property<TObj, TProperty> GetProperty()
        {
            return _memberMap.GetProperty();
        }

        internal AttributeGroupTypeMap<TProperty> GetGroupTypeMap()
        {
            if (_groupTypeMap == null)
            {
                throw new OXMException("Attribute group type map is not set for property.");
            }

            return _groupTypeMap;
        }
    }
    
    public class ClassMemberMap<TObj, TProperty>
    {
        ClassMap<TProperty> _classMap;
        MemberMap<TObj, TProperty> _memberMap;
       
        public ClassMemberMap(Expression<Func<TObj, TProperty>> property)
        {
            _memberMap = new MemberMap<TObj, TProperty>(property);
        }

        public ClassMemberMap<TObj, TProperty> Set(Action<TProperty> set)
        {
            _memberMap.Set(set);
            return this;
        }

        public void ClassMap(ClassMap<TProperty> classMap)
        {
            _classMap = classMap;
        }

        internal Property<TObj, TProperty> GetProperty()
        {
            return _memberMap.GetProperty();
        }

        internal ClassMap<TProperty> GetClassMap()
        {
            if (_classMap == null)
            {
                throw new OXMException("Class map is not set for property.");
            }

            return _classMap;
        }
    }
}