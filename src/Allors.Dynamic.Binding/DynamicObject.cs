﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using Allors.Dynamic.Meta;

namespace Allors.Dynamic.Binding
{
    public sealed class DynamicObject : System.Dynamic.DynamicObject, IDynamicObject
    {
        internal DynamicObject(DynamicPopulation population, DynamicObjectType objectType)
        {
            Population = population;
            ObjectType = objectType;
            this.Database = population.Database;
        }

        IDynamicPopulation IDynamicObject.Population => this.Population;

        public DynamicPopulation Population { get; }

        public DynamicObjectType ObjectType { get; }

        internal DynamicDatabase Database { get; }

        object IDynamicObject.GetRole(DynamicUnitRoleType roleType) => this.GetRole(roleType);

        IDynamicObject IDynamicObject.GetRole(IDynamicToOneRoleType roleType) => this.GetRole(roleType);

        IReadOnlyList<IDynamicObject> IDynamicObject.GetRole(IDynamicToManyRoleType roleType) => this.GetRole(roleType);

        void IDynamicObject.SetRole(DynamicUnitRoleType roleType, object value) => this.SetRole(roleType, value);

        void IDynamicObject.SetRole(IDynamicToOneRoleType roleType, IDynamicObject value) => this.SetRole(roleType, (DynamicObject)value);

        void IDynamicObject.SetRole(IDynamicToManyRoleType roleType, IEnumerable<IDynamicObject> value) => this.SetRole(roleType, value);

        void IDynamicObject.AddRole(IDynamicToManyRoleType roleType, IDynamicObject role) => this.AddRole(roleType, (DynamicObject)role);

        void IDynamicObject.RemoveRole(IDynamicToManyRoleType roleType, IDynamicObject role) => this.RemoveRole(roleType, (DynamicObject)role);

        IDynamicObject IDynamicObject.GetAssociation(IDynamicOneToAssociationType associationType) => this.GetAssociation(associationType);

        IReadOnlyList<IDynamicObject> IDynamicObject.GetAssociation(IDynamicManyToAssociationType associationType) => this.GetAssociation(associationType);

        public object GetRole(DynamicUnitRoleType roleType) => this.Database.GetRole(this, roleType);

        public DynamicObject GetRole(IDynamicToOneRoleType roleType) => (DynamicObject)this.Database.GetRole(this, roleType);

        public IReadOnlyList<DynamicObject> GetRole(IDynamicToManyRoleType roleType) => (IReadOnlyList<DynamicObject>)this.Database.GetRole(this, roleType) ?? [];

        public void SetRole(DynamicUnitRoleType roleType, object value) => this.Database.SetRole(this, roleType, value);

        public void SetRole(IDynamicToOneRoleType roleType, DynamicObject value) => this.Database.SetRole(this, roleType, value);

        public void SetRole(IDynamicToOneRoleType roleType, IEnumerable<DynamicObject> value) => this.Database.SetRole(this, roleType, value);

        public void AddRole(IDynamicToManyRoleType roleType, DynamicObject role) => this.Database.AddRole(this, roleType, role);

        public void RemoveRole(IDynamicToManyRoleType roleType, DynamicObject role) => this.Database.RemoveRole(this, roleType, role);

        public DynamicObject GetAssociation(IDynamicOneToAssociationType associationType) => (DynamicObject)this.Database.GetAssociation(this, associationType);

        public IReadOnlyList<DynamicObject> GetAssociation(IDynamicManyToAssociationType associationType) => (IReadOnlyList<DynamicObject>)this.Database.GetAssociation(this, associationType) ?? [];

        /// <inheritdoc/>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            return TryGet(indexes[0], out result);
        }

        /// <inheritdoc/>
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            return TrySet(indexes[0], value);
        }

        /// <inheritdoc/>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryGet(binder.Name, out result);
        }

        /// <inheritdoc/>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return TrySet(binder.Name, value);
        }

        /// <inheritdoc/>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var name = binder.Name;

            result = null;

            if (name.StartsWith("Add", StringComparison.InvariantCulture) && ObjectType.RoleTypeByName.TryGetValue(name.Substring(3), out var roleType))
            {
                this.AddRole((IDynamicToManyRoleType)roleType, (DynamicObject)args[0]);
                return true;
            }

            if (name.StartsWith("Remove", StringComparison.InvariantCulture) && ObjectType.RoleTypeByName.TryGetValue(name.Substring(6), out roleType))
            {
                // TODO: RemoveAll
                this.RemoveRole((IDynamicToManyRoleType)roleType, (DynamicObject)args[0]);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var roleType in this.ObjectType.RoleTypeByName.Values.ToArray().Distinct())
            {
                yield return roleType.Name;
            }

            foreach (var associationType in this.ObjectType.AssociationTypeByName.Values.ToArray().Distinct())
            {
                yield return associationType.Name;
            }
        }

        private bool TryGet(object nameOrType, out object result)
        {
            while (true)
            {
                switch (nameOrType)
                {
                    case string name:
                        if (ObjectType.RoleTypeByName.TryGetValue(name, out var roleType))
                        {
                            nameOrType = roleType;
                            continue;
                        }

                        if (ObjectType.AssociationTypeByName.TryGetValue(name, out var associationType))
                        {
                            nameOrType = (IDynamicCompositeAssociationType)associationType;
                            continue;
                        }

                        break;

                    case DynamicUnitRoleType unitRoleType:
                        result = this.GetRole(unitRoleType);
                        return true;

                    case IDynamicToOneRoleType toOneRoleType:
                        result = this.GetRole(toOneRoleType);
                        return true;

                    case IDynamicToManyRoleType toManyRoleType:
                        result = this.GetRole(toManyRoleType);
                        return true;

                    case IDynamicOneToAssociationType oneToAssociationType:
                        result = this.GetAssociation(oneToAssociationType);
                        return true;

                    case IDynamicManyToAssociationType oneToAssociationType:
                        result = this.GetAssociation(oneToAssociationType);
                        return true;
                }

                result = null;
                return false;
            }
        }

        private bool TrySet(object nameOrType, object value)
        {
            switch (nameOrType)
            {
                case string name:
                    {
                        if (ObjectType.RoleTypeByName.TryGetValue(name, out var roleType))
                        {
                            return TrySet(roleType, value);
                        }
                    }

                    break;

                case DynamicUnitRoleType roleType:
                    this.SetRole(roleType, value);
                    return true;

                case IDynamicToOneRoleType roleType:
                    this.SetRole(roleType, (DynamicObject)value);
                    return true;

                case IDynamicToManyRoleType roleType:
                    this.SetRole(roleType, (System.Collections.IEnumerable)value);
                    return true;
            }

            return false;
        }

        private void SetRole(IDynamicToManyRoleType roleType, System.Collections.IEnumerable value) => this.Database.SetRole(this, roleType, value);

    }
}