﻿namespace Allors.Dynamic.Tests.Domain
{
    using Allors.Dynamic.Domain;
    using Allors.Dynamic.Meta;
    using Xunit;

    public class DynamicPopulationTests
    {
        [Fact]
        public void New()
        {
            var meta = new DynamicMeta();
            var named = meta.AddInterface("Named");
            var organization = meta.AddClass("Organization", named);
            var person = meta.AddClass("Person", named);
            meta.AddUnit<string>(named, "Name");
            meta.AddOneToOne(organization, person, "Owner");

            var population = new DynamicPopulation();

            var acme = population.Create(organization, v =>
            {
                v["Name"] = "Acme";
                v["Owner"] = population.Create(person, w => w["Name"] = "Jane");
            });

            var jane = (DynamicObject)acme["Owner"]!;

            Assert.Equal("Acme", acme["Name"]);
            Assert.Equal("Jane", jane["Name"]);

            Assert.Equal(acme, jane["OrganizationWhereOwner"]);
        }
    }
}
