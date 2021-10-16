using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.NUnit3;

using LUC.DiscoveryService.Test.Builders;
using LUC.DiscoveryService.Test.InternalTests.Customizations;

namespace LUC.DiscoveryService.Test.InternalTests.Attributes
{
    internal class ConnectionPoolSocketConventionsAttribute : AutoDataAttribute
    {
        internal ConnectionPoolSocketConventionsAttribute( BuildEndPointRequest request )
            : base( () =>
            {
                Fixture specimens = new Fixture();
                return specimens.Customize( new ConnectionPoolSocketBasicCustomization( request ) );
            } )
        {
            ;//do nothing
        }
    }
}
