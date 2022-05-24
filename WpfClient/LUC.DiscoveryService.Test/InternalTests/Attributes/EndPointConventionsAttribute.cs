using AutoFixture;
using AutoFixture.NUnit3;

using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Customizations;

namespace LUC.DiscoveryServices.Test.InternalTests.Attributes
{
    class EndPointConventionsAttribute : AutoDataAttribute
    {
        public EndPointConventionsAttribute( BuildEndPointRequest buildEndPointRequest )
            : base( () =>
             {
                 IFixture fixture = new Fixture();
                 IFixture customizedFixture = fixture.Customize( new EndPointCustomization( buildEndPointRequest ) );
                 return customizedFixture;
             } )
        {
            ;//do nothing
        }
    }
}
