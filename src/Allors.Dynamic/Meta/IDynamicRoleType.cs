﻿namespace Allors.Dynamic.Meta
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Allors.Dynamic.Domain;

    public interface IDynamicRoleType
    {
        IDynamicAssociationType AssociationType { get; }

        DynamicObjectType ObjectType { get; }

        string SingularName { get; }

        string PluralName { get; }

        string Name { get; }

        void Deconstruct(out IDynamicRoleType roleType, out IDynamicAssociationType associationType);

        internal object? Normalize(object? value) =>
            this switch
            {
                DynamicUnitRoleType => this.NormalizeUnit(value),
                IDynamicToOneRoleType => this.NormalizeToOne(value),
                _ => this.NormalizeToMany(value),
            };

        private object? NormalizeUnit(object? value)
        {
            if (value == null)
            {
                return value;
            }

            if (value is DateTime dateTime && dateTime != DateTime.MinValue && dateTime != DateTime.MaxValue)
            {
                dateTime = dateTime.Kind switch
                {
                    DateTimeKind.Local => dateTime.ToUniversalTime(),
                    DateTimeKind.Unspecified => throw new ArgumentException(@"DateTime value is of DateTimeKind.Kind Unspecified.
Unspecified is only allowed for DateTime.MaxValue and DateTime.MinValue. 
Use DateTimeKind.Utc or DateTimeKind.Local."),
                    _ => dateTime,
                };

                return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Utc);
            }

            if (value.GetType() != this.ObjectType.Type && this.ObjectType.TypeCode.HasValue)
            {
                value = Convert.ChangeType(value, this.ObjectType.TypeCode.Value, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private object? NormalizeToOne(object? value)
        {
            if (value is not null)
            {
                if (value is DynamicObject dynamicObject)
                {
                    if (!this.ObjectType.IsAssignableFrom(dynamicObject.ObjectType))
                    {
                        throw new ArgumentException($"{this.Name} should be assignable to {this.ObjectType.Name} but was a {dynamicObject.ObjectType.Name}");
                    }
                }
                else
                {
                    throw new ArgumentException($"{this.Name} should be a dynamic object but was a {value.GetType()}");
                }
            }

            return value;
        }

        private object? NormalizeToMany(object? value)
        {
            return value switch
            {
                null => null,
                ICollection collection => this.NormalizeToMany(collection).ToArray(),
                _ => throw new ArgumentException($"{value.GetType()} is not a collection Type"),
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
                        if (!this.ObjectType.IsAssignableFrom(dynamicObject.ObjectType))
                        {
                            throw new ArgumentException($"{this.Name} should be assignable to {this.ObjectType.Name} but was a {dynamicObject.ObjectType.Name}");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"{this.Name} should be a dynamic object but was a {@object.GetType()}");
                    }

                    yield return dynamicObject;
                }
            }
        }
    }
}
