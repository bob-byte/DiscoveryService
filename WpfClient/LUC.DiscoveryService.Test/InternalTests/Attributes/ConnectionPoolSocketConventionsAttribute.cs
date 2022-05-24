
using AutoFixture;
using AutoFixture.NUnit3;

using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Customizations;

namespace LUC.DiscoveryServices.Test.InternalTests.Attributes
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
