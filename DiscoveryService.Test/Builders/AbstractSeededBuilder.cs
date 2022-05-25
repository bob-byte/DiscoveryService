using System;

using AutoFixture.Kernel;

namespace LUC.DiscoveryServices.Test.Builders
{
    abstract class AbstractSeededBuilder<T> : ISpecimenBuilder
        where T : struct
    {
        protected AbstractSeededBuilder( T request )
        {
            Request = request;
        }

        protected T Request { get; set; }

        public abstract Object Create( Object fixtureRequest, ISpecimenContext specimenContext );

        protected abstract Boolean IsRightRequest( Object fixtureRequest );

        protected Type RequestType( Object request ) =>
            ( request is SeededRequest seededRequest ) && ( seededRequest.Request is Type requestType )
                ? requestType
                : throw new IllegalRequestException( $"{nameof(request)} isn\'t {nameof(Type)}" );
    }
}
