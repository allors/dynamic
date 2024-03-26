﻿
namespace Allors.Dynamic.Meta
{
    public sealed class DynamicManyToOneRoleType : IDynamicToOneRoleType
    {
        internal DynamicManyToOneRoleType(DynamicObjectType objectType, string singularName, string pluralName, string name)
        {
            this.ObjectType = objectType;
            this.SingularName = singularName;
            this.PluralName = pluralName;
            this.Name = name;
        }

        IDynamicAssociationType IDynamicRoleType.AssociationType => this.AssociationType;

        public DynamicManyToOneAssociationType AssociationType { get; internal set; } = null!;

        public DynamicObjectType ObjectType { get; }

        public string SingularName { get; }

        public string PluralName { get; }

        public string Name { get; }

        public bool IsOne => true;

        public bool IsMany => false;

        void IDynamicRoleType.Deconstruct(out IDynamicRoleType roleType, out IDynamicAssociationType associationType)
        {
            associationType = this.AssociationType;
            roleType = this;
        }

        public void Deconstruct(out DynamicManyToOneRoleType roleType, out DynamicManyToOneAssociationType associationType)
        {
            associationType = this.AssociationType;
            roleType = this;
        }

        public override string ToString()
        {
            return this.Name;
        }

        internal string SingularNameForAssociationType(DynamicObjectType dynamicObjectType)
        {
            return $"{dynamicObjectType.Name}Where{this.SingularName}";
        }

        internal string PluralNameForAssociationType(DynamicObjectType dynamicObjectType)
        {
            return $"{this.ObjectType.Meta.Pluralize(dynamicObjectType.Name)}Where{this.SingularName}";
        }
    }
}
