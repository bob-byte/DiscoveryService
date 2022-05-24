using AutoFixture;
using AutoFixture.NUnit3;

using DiscoveryServices.Test.Builders;
using DiscoveryServices.Test.InternalTests.Customizations;

namespace DiscoveryServices.Test.InternalTests.Attributes
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
