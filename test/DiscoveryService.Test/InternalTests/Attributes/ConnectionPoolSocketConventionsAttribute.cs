using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AutoFixture;
using AutoFixture.NUnit3;

using LUC.DiscoveryServices.Test.Builders;
using LUC.DiscoveryServices.Test.InternalTests.Customizations;

namespace LUC.DiscoveryServices.Test.InternalTests.Attributes
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
