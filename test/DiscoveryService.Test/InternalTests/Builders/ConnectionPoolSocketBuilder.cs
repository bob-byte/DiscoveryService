using System;
using System.Net;
using System.Reflection;

using AutoFixture.Kernel;

using LUC.Interfaces;

namespace LUC.DiscoveryServices.Test.InternalTests.Builders
{
    class ConnectionPoolSocketBuilder : ISpecimenBuilder
    {
        private readonly ILoggingService m_log;
        private readonly EndPoint m_endPoint;

        public ConnectionPoolSocketBuilder( ILoggingService log, EndPoint endPoint )
            : this(log)
        {
            m_endPoint = endPoint;
        }

        public ConnectionPoolSocketBuilder(ILoggingService log)
        {
            m_log = log;
        }

        public Object Create(Object request, ISpecimenContext specimenContext)
        {
            Object parameterValue = null;

            if(request is ParameterInfo parameter)
            {
                if(parameter.Name == "log" && parameter.ParameterType == typeof(ILoggingService))
                {
                    parameterValue = m_log;
                }
                else if(parameter.Name == "remoteEndPoint" && parameter.ParameterType == typeof(EndPoint) )
                {
                    parameterValue = m_endPoint;
                }
            }

            if ( parameterValue != null )
            {
                return parameterValue;
            }
            else
            {
                return new NoSpecimen();
            }
        }
    }
}
