namespace Allors.Dynamic.Tests
{
    using System;
    using Xunit;

    public class BuilderTests
    {
        [Fact]
        public void Set()
        {
            var population = new DynamicPopulation(v => v
                .AddDataAssociation("Name")
                .AddOneToOneAssociation("Property", "Owner")
             );

            dynamic Create(params Action<dynamic>[] builders) => population.Create(builders);

            Action<dynamic> Name(string name) => (obj) => obj.Name = name;
            Action<dynamic> Owner(dynamic owner) => (obj) => obj.Owner = owner;

            dynamic acme = Create(Name("Acme"), Owner(Create(Name("Jane"))));

            Assert.Equal("Acme", acme.Name);

            var jane = acme.Owner;
            Assert.Equal("Jane", jane.Name);
        }
    }
}