﻿namespace Allors.Dynamic.Domain
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Allors.Dynamic.Meta;

    public sealed class DynamicPopulation
    {
        private readonly Dictionary<IDynamicRoleType, Dictionary<DynamicObject, object>> roleByAssociationByRoleType = [];
        private readonly Dictionary<IDynamicCompositeAssociationType, Dictionary<DynamicObject, object>> associationByRoleByAssociationType = [];

        private Dictionary<IDynamicRoleType, Dictionary<DynamicObject, object>> changedRoleByAssociationByRoleType = [];
        private Dictionary<IDynamicCompositeAssociationType, Dictionary<DynamicObject, object>> changedAssociationByRoleByAssociationType = [];

        private IImmutableList<DynamicObject> objects = ImmutableArray<DynamicObject>.Empty;

        public Dictionary<string, IDynamicDerivation> DerivationById { get; } = [];

        public IReadOnlyList<DynamicObject> Objects => this.objects;

        public DynamicObject Create(DynamicObjectType @class, params Action<DynamicObject>[] builders)
        {
            var @new = new DynamicObject(this, @class);
            this.objects = this.objects.Add(@new);

            foreach (var builder in builders)
            {
                builder(@new);
            }

            return @new;
        }

        public DynamicChangeSet Snapshot()
        {
            foreach (var roleType in this.changedRoleByAssociationByRoleType.Keys.ToArray())
            {
                var changedRoleByAssociation = this.changedRoleByAssociationByRoleType[roleType];
                var roleByAssociation = this.RoleByAssociation(roleType);

                foreach (var association in changedRoleByAssociation.Keys.ToArray())
                {
                    var role = changedRoleByAssociation[association];
                    roleByAssociation.TryGetValue(association, out var originalRole);

                    var compositeRoleType = roleType as IDynamicCompositeRoleType;

                    var areEqual = ReferenceEquals(originalRole, role) ||
                                   (compositeRoleType?.IsOne == true && Equals(originalRole, role)) ||
                                   (compositeRoleType?.IsMany == true && Same(originalRole, role));

                    if (areEqual)
                    {
                        changedRoleByAssociation.Remove(association);
                        continue;
                    }

                    roleByAssociation[association] = role;
                }

                if (roleByAssociation.Count == 0)
                {
                    this.changedRoleByAssociationByRoleType.Remove(roleType);
                }
            }

            foreach (var associationType in this.changedAssociationByRoleByAssociationType.Keys.ToArray())
            {
                var changedAssociationByRole = this.changedAssociationByRoleByAssociationType[associationType];
                var associationByRole = this.AssociationByRole(associationType);

                foreach (var role in changedAssociationByRole.Keys.ToArray())
                {
                    var changedAssociation = changedAssociationByRole[role];
                    associationByRole.TryGetValue(role, out var originalAssociation);

                    var areEqual = ReferenceEquals(originalAssociation, changedAssociation) ||
                                   (associationType.IsOne && Equals(originalAssociation, changedAssociation)) ||
                                   (associationType.IsMany && Same(originalAssociation, changedAssociation));

                    if (areEqual)
                    {
                        changedAssociationByRole.Remove(role);
                        continue;
                    }

                    associationByRole[role] = changedAssociation;
                }

                if (associationByRole.Count == 0)
                {
                    this.changedAssociationByRoleByAssociationType.Remove(associationType);
                }
            }

            var snapshot = new DynamicChangeSet(this.changedRoleByAssociationByRoleType, this.changedAssociationByRoleByAssociationType);

            foreach (var kvp in this.changedRoleByAssociationByRoleType)
            {
                var roleType = kvp.Key;
                var changedRoleByAssociation = kvp.Value;

                var roleByAssociation = this.RoleByAssociation(roleType);

                foreach (var kvp2 in changedRoleByAssociation)
                {
                    var association = kvp2.Key;
                    var changedRole = kvp2.Value;
                    roleByAssociation[association] = changedRole;
                }
            }

            foreach (var kvp in this.changedAssociationByRoleByAssociationType)
            {
                var associationType = kvp.Key;
                var changedAssociationByRole = kvp.Value;

                var associationByRole = this.AssociationByRole(associationType);

                foreach (var kvp2 in changedAssociationByRole)
                {
                    var role = kvp2.Key;
                    var changedAssociation = kvp2.Value;
                    associationByRole[role] = changedAssociation;
                }
            }

            this.changedRoleByAssociationByRoleType = [];
            this.changedAssociationByRoleByAssociationType = [];

            return snapshot;
        }

        public void Derive()
        {
            var changeSet = this.Snapshot();

            while (changeSet.HasChanges)
            {
                foreach (var kvp in this.DerivationById)
                {
                    var derivation = kvp.Value;
                    derivation.Derive(changeSet);
                }

                changeSet = this.Snapshot();
            }
        }

        internal object? GetRole(DynamicObject association, IDynamicRoleType roleType)
        {
            if (this.changedRoleByAssociationByRoleType.TryGetValue(roleType, out var changedRoleByAssociation) &&
                changedRoleByAssociation.TryGetValue(association, out var role))
            {
                return role;
            }

            this.RoleByAssociation(roleType).TryGetValue(association, out role);
            return role;
        }

        internal void SetUnitRole(DynamicObject association, IDynamicRoleType roleType, object? role)
        {
            var normalizedRole = roleType.Normalize(role);

            if (normalizedRole == null)
            {
                this.RemoveFromRole(association, roleType);
                return;
            }

            // Role
            this.ChangedRoleByAssociation(roleType)[association] = normalizedRole;
        }

        internal void SetToOneRole(DynamicObject association, IDynamicRoleType roleType, object? role)
        {
            var normalizedRole = roleType.Normalize(role);

            if (normalizedRole == null)
            {
                this.RemoveFromRole(association, roleType);
                return;
            }

            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;
            var previousRole = this.GetRole(association, roleType);

            var roleObject = (DynamicObject)normalizedRole;

            // Role
            var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
            changedRoleByAssociation[association] = roleObject;

            // Association
            var changedAssociationByRole = this.ChangedAssociationByRole(associationType);
            if (associationType.IsOne)
            {
                var previousAssociation = this.GetAssociation(roleObject, associationType);

                // One to One
                var previousAssociationObject = (DynamicObject?)previousAssociation;
                if (previousAssociationObject != null)
                {
                    changedRoleByAssociation.Remove(previousAssociationObject);
                }

                if (previousRole != null)
                {
                    var previousRoleObject = (DynamicObject)previousRole;
                    changedAssociationByRole.Remove(previousRoleObject);
                }

                changedAssociationByRole[roleObject] = association;
            }
            else
            {
                // Many to One
                var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(roleObject, associationType);
                if (previousAssociation?.Contains(roleObject) == true)
                {
                    changedAssociationByRole[roleObject] = previousAssociation.Remove(roleObject);
                }
            }
        }

        internal void SetToManyRole(DynamicObject association, IDynamicRoleType roleType, object? role)
        {
            var normalizedRole = roleType.Normalize(role);

            if (normalizedRole == null)
            {
                this.RemoveFromRole(association, roleType);
                return;
            }

            var previousRole = this.GetRole(association, roleType);

            var roles = ((IEnumerable)normalizedRole).Cast<DynamicObject>().ToArray();
            var previousRoles = (IImmutableSet<DynamicObject>?)previousRole;

            if (previousRoles != null)
            {
                // Use Diff (Add/Remove)
                var addedRoles = roles.Except(previousRoles);
                var removedRoles = previousRoles.Except(roles);

                foreach (var addedRole in addedRoles)
                {
                    this.AddToRole(association, roleType, addedRole);
                }

                foreach (var removeRole in removedRoles)
                {
                    this.RemoveFromRole(association, roleType, removeRole);
                }
            }
            else
            {
                foreach (var addedRole in roles)
                {
                    this.AddToRole(association, roleType, addedRole);
                }
            }
        }

        internal void AddToRole(DynamicObject association, IDynamicRoleType roleType, DynamicObject item)
        {
            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;

            // Role
            var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
            var previousRole = (IImmutableSet<DynamicObject>?)this.GetRole(association, roleType);
            var newRole = previousRole != null ? previousRole.Add(item) : ImmutableHashSet.Create(item);
            changedRoleByAssociation[association] = newRole;

            // Association
            var changedAssociationByRole = this.ChangedAssociationByRole(associationType);
            if (associationType.IsOne)
            {
                var previousAssociation = (DynamicObject?)this.GetAssociation(item, associationType);

                // One to Many
                if (previousAssociation != null)
                {
                    var previousAssociationRole = (IImmutableSet<DynamicObject>?)this.GetRole(previousAssociation, roleType);
                    if (previousAssociationRole?.Contains(item) == true)
                    {
                        changedRoleByAssociation[previousAssociation] = previousAssociationRole.Remove(item);
                    }
                }

                changedAssociationByRole[item] = association;
            }
            else
            {
                var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(item, associationType);

                // Many to Many
                changedAssociationByRole[item] = previousAssociation != null ? previousAssociation.Add(association) : ImmutableHashSet.Create(association);
            }
        }

        internal void AddToRole(DynamicObject association, IDynamicRoleType roleType, DynamicObject[]? items)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;

            // Role
            var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
            var previousRole = (IImmutableSet<DynamicObject>?)this.GetRole(association, roleType);
            changedRoleByAssociation[association] = previousRole != null ? previousRole.Union(items) : ImmutableHashSet.Create(items);

            // Association
            var changedAssociationByRole = this.ChangedAssociationByRole(associationType);
            foreach (var item in items)
            {
                if (associationType.IsOne)
                {
                    var previousAssociation = (DynamicObject?)this.GetAssociation(item, associationType);

                    // One to Many
                    if (previousAssociation != null)
                    {
                        var previousAssociationRole = (IImmutableSet<DynamicObject>?)this.GetRole(previousAssociation, roleType);
                        if (previousAssociationRole?.Contains(item) == true)
                        {
                            changedRoleByAssociation[previousAssociation] = previousAssociationRole.Remove(item);
                        }
                    }

                    changedAssociationByRole[item] = association;
                }
                else
                {
                    var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(item, associationType);

                    // Many to Many
                    changedAssociationByRole[item] = previousAssociation != null ? previousAssociation.Add(association) : ImmutableHashSet.Create(association);
                }
            }
        }

        internal void RemoveFromRole(DynamicObject association, IDynamicRoleType roleType, DynamicObject item)
        {
            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;

            var previousRole = (IImmutableSet<DynamicObject>?)this.GetRole(association, roleType);
            if (previousRole?.Contains(item) == true)
            {
                // Role
                var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
                changedRoleByAssociation[association] = previousRole.Remove(item);

                // Association
                var changedAssociationByRole = this.ChangedAssociationByRole(associationType);
                if (associationType.IsOne)
                {
                    // One to Many
                    changedAssociationByRole.Remove(item);
                }
                else
                {
                    var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(item, associationType);

                    // Many to Many
                    if (previousAssociation?.Contains(association) == true)
                    {
                        changedAssociationByRole[item] = previousAssociation.Remove(association);
                    }
                }
            }
        }

        internal void RemoveFromRole(DynamicObject association, IDynamicRoleType roleType, DynamicObject[]? items)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;

            var previousRole = (IImmutableSet<DynamicObject>?)this.GetRole(association, roleType);
            if (previousRole?.Overlaps(items) == true)
            {
                // Role
                var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
                changedRoleByAssociation[association] = previousRole.Except(items);

                // Association
                var changedAssociationByRole = this.ChangedAssociationByRole(associationType);

                foreach (var item in items)
                {
                    if (associationType.IsOne)
                    {
                        // One to Many
                        changedAssociationByRole.Remove(item);
                    }
                    else
                    {
                        var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(item, associationType);

                        // Many to Many
                        if (previousAssociation?.Contains(association) == true)
                        {
                            changedAssociationByRole[item] = previousAssociation.Remove(association);
                        }
                    }
                }
            }
        }

        internal object? GetAssociation(DynamicObject role, IDynamicCompositeAssociationType associationType)
        {
            if (this.changedAssociationByRoleByAssociationType.TryGetValue(associationType, out var changedAssociationByRole) &&
                changedAssociationByRole.TryGetValue(role, out var association))
            {
                return association;
            }

            this.AssociationByRole(associationType).TryGetValue(role, out association);
            return association;
        }

        private static bool Same(object? source, object? destination)
        {
            if (source == null && destination == null)
            {
                return true;
            }

            if (source == null || destination == null)
            {
                return false;
            }

            if (source is IReadOnlySet<DynamicObject> sourceSet)
            {
                return sourceSet.SetEquals((IEnumerable<DynamicObject>)destination);
            }

            var destinationSet = (IReadOnlySet<DynamicObject>)destination;
            return destinationSet.SetEquals((IEnumerable<DynamicObject>)source);
        }

        private Dictionary<DynamicObject, object> AssociationByRole(IDynamicCompositeAssociationType associationType)
        {
            if (!this.associationByRoleByAssociationType.TryGetValue(associationType, out var associationByRole))
            {
                associationByRole = [];
                this.associationByRoleByAssociationType[associationType] = associationByRole;
            }

            return associationByRole;
        }

        private Dictionary<DynamicObject, object> RoleByAssociation(IDynamicRoleType roleType)
        {
            if (!this.roleByAssociationByRoleType.TryGetValue(roleType, out var roleByAssociation))
            {
                roleByAssociation = [];
                this.roleByAssociationByRoleType[roleType] = roleByAssociation;
            }

            return roleByAssociation;
        }

        private Dictionary<DynamicObject, object> ChangedAssociationByRole(IDynamicCompositeAssociationType associationType)
        {
            if (!this.changedAssociationByRoleByAssociationType.TryGetValue(associationType, out var changedAssociationByRole))
            {
                changedAssociationByRole = [];
                this.changedAssociationByRoleByAssociationType[associationType] = changedAssociationByRole;
            }

            return changedAssociationByRole;
        }

        private Dictionary<DynamicObject, object> ChangedRoleByAssociation(IDynamicRoleType roleType)
        {
            if (!this.changedRoleByAssociationByRoleType.TryGetValue(roleType, out var changedRoleByAssociation))
            {
                changedRoleByAssociation = [];
                this.changedRoleByAssociationByRoleType[roleType] = changedRoleByAssociation;
            }

            return changedRoleByAssociation;
        }

        private void RemoveFromRole(DynamicObject association, IDynamicRoleType roleType)
        {
            var associationType = (IDynamicCompositeAssociationType)roleType.AssociationType;

            var previousRole = (IImmutableSet<DynamicObject>?)this.GetRole(association, roleType);
            if (previousRole != null)
            {
                // Role
                var changedRoleByAssociation = this.ChangedRoleByAssociation(roleType);
                changedRoleByAssociation.Remove(association);

                // Association
                var changedAssociationByRole = this.ChangedAssociationByRole(associationType);
                foreach (var role in previousRole)
                {
                    if (associationType.IsOne)
                    {
                        // One to Many
                        changedAssociationByRole.Remove(role);
                    }
                    else
                    {
                        var previousAssociation = (IImmutableSet<DynamicObject>?)this.GetAssociation(role, associationType);

                        // Many to Many
                        if (previousAssociation?.Contains(association) == true)
                        {
                            changedAssociationByRole[role] = previousAssociation.Remove(association);
                        }
                    }
                }
            }
        }
    }
}
