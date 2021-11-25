using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using AutoFixture.Kernel;

namespace LUC.DiscoveryServices.Test.Builders
{
    abstract class AbstractSeededBuilder<T> : ISpecimenBuilder
        where T: struct
    {
        public AbstractSeededBuilder(T request)
        {
            Request = request;
        }

        public T Request { get; set; }

        public abstract Object Create( Object fixtureRequest, ISpecimenContext specimenContext );
        
        protected abstract Boolean IsRightRequest( Object fixtureRequest );

        protected virtual Type RequestType( Object request )
        {
            if ( ( request is SeededRequest seededRequest ) && ( seededRequest.Request is Type requestType) )
            {
                return requestType;
            }
            else
            {
                throw new IllegalRequestException( $"{nameof( request )} isn\'t {nameof( Type )}" );
            }
        }
    }
}
