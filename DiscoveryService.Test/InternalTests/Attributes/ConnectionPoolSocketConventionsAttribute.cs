
using AutoFixture;
using AutoFixture.NUnit3;

using DiscoveryServices.Test.Builders;
using DiscoveryServices.Test.InternalTests.Customizations;

namespace DiscoveryServices.Test.InternalTests.Attributes
{
    internal class SocketConventionsAttribute : AutoDataAttribute
    {
        internal SocketConventionsAttribute( BuildEndPointRequest request )
            : base( () =>
            {
                var specimens = new Fixture();
                return specimens.Customize( new SocketBasicCustomization( request ) );
            } )
        {
            ;//do nothing
        }
    }
}
