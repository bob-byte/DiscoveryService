using AutoFixture;

using LUC.DiscoveryServices.Test.Builders;

namespace LUC.DiscoveryServices.Test.InternalTests.Customizations
{
    class EndPointCustomization : ICustomization
    {
        private readonly BuildEndPointRequest m_endPointRequest;

        public EndPointCustomization( BuildEndPointRequest endPointRequest )
        {
            m_endPointRequest = endPointRequest;
        }

        public virtual void Customize( IFixture fixture ) =>
            fixture.Customizations.Add( new EndPointsBuilder( m_endPointRequest ) );
    }
}
