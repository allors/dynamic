﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Allors.Dynamic.Meta
{
    public class DynamicRoleType
    {
        public DynamicAssociationType AssociationType { get; internal set; } = null!;

        public DynamicObjectType ObjectType { get; }

        public string SingularName { get; }

        public string PluralName { get; }

        public string Name { get; }

        public bool IsOne { get; }

        public bool IsMany { get; }

        public bool IsUnit { get; }

        public void Deconstruct(out DynamicAssociationType associationType, out DynamicRoleType roleType)
        {
            associationType = AssociationType;
            roleType = this;
        }

        internal DynamicRoleType(DynamicObjectType objectType, string singularName, string pluralName, string name, bool isOne, bool isMany, bool isUnit)
        {
            ObjectType = objectType;
            SingularName = singularName;
            PluralName = pluralName;
            Name = name;
            IsOne = isOne;
            IsMany = isMany;
            IsUnit = isUnit;
        }

        public override string ToString()
        {
            return Name;
        }

        internal string SingularNameForEmbeddedAssociationType(DynamicObjectType dynamicObjectType)
        {
            return $"{dynamicObjectType.Name}Where{SingularName}";
        }

        internal string PluralNameForEmbeddedAssociationType(DynamicObjectType dynamicObjectType)
        {
            return $"{ObjectType.Meta.Pluralize(dynamicObjectType.Name)}Where{SingularName}";
        }

        internal object? Normalize(object? value)
        {
            if (IsUnit)
            {
                return NormalizeUnit(value);
            }

            return IsOne switch
            {
                true => NormalizeToOne(value),
                _ => NormalizeToMany(value)
            };
        }

        private object NormalizeUnit(object value)
        {
            if (value == null)
            {
                return value;
            }

            if (value is DateTime dateTime && dateTime != DateTime.MinValue && dateTime != DateTime.MaxValue)
            {
                switch (dateTime.Kind)
                {
                    case DateTimeKind.Local:
                        dateTime = dateTime.ToUniversalTime();
                        break;
                    case DateTimeKind.Unspecified:
                        throw new ArgumentException(
                            @"DateTime value is of DateTimeKind.Kind Unspecified.
Unspecified is only allowed for DateTime.MaxValue and DateTime.MinValue. 
Use DateTimeKind.Utc or DateTimeKind.Local.");
                }

                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Utc);
            }

            if (value.GetType() != ObjectType.Type)
            {
                value = Convert.ChangeType(value, ObjectType.TypeCode);
            }

            return value;
        }

        private object? NormalizeToOne(object? value)
        {
            if (value is not null)
            {
                if (value is DynamicObject dynamicObject)
                {
                    if (!dynamicObject.ObjectType.IsAssignableTo(this.ObjectType))
                    {
                        throw new ArgumentException($"{Name} should be assignable to {this.ObjectType.Name} but was a {dynamicObject.ObjectType.Name}");
                    }
                }
                else
                {
                    throw new ArgumentException($"{Name} should be a dynamic object but was a {value.GetType()}");
                }
            }

            return value;
        }

        private object? NormalizeToMany(object? value)
        {
            return value switch
            {
                null => null,
                ICollection collection => NormalizeToMany(collection).ToArray(),
                _ => throw new ArgumentException($"{value.GetType()} is not a collection Type")
            };
        }

        private IEnumerable<DynamicObject> NormalizeToMany(ICollection role)
        {
            foreach (var @object in role)
            {
                if (@object != null)
                {
                    if (@object is DynamicObject dynamicObject)
                    {
                        if (!dynamicObject.ObjectType.IsAssignableTo(this.ObjectType))
                        {
                            throw new ArgumentException($"{Name} should be assignable to {this.ObjectType.Name} but was a {dynamicObject.ObjectType.Name}");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"{Name} should be a dynamic object but was a {@object.GetType()}");
                    }

                    yield return dynamicObject;
                }
            }
        }
    }
}