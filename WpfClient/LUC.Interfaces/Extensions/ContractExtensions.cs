using LUC.Interfaces.Models;
using LUC.Interfaces.OutputContracts;

using System;
using System.Collections.Generic;
using System.Text;

namespace LUC.Interfaces.Extensions
{
    public static class ContractExtensions
    {
        public static List<GroupServiceModel> ToGroupServiceModelList( this List<GroupSubResponse> groups )
        {
            var result = new List<GroupServiceModel>();

            foreach ( GroupSubResponse group in groups )
            {
                // read the string as UTF-8 bytes.
                Byte[] encodedUtf8Bytes = Encoding.UTF8.GetBytes( group.Name );

                // convert them into unicode bytes.
                Byte[] unicodeBytes = Encoding.Convert( Encoding.UTF8, Encoding.Unicode, encodedUtf8Bytes );

                // builds the converted string.
                String unicodeGroupName = Encoding.Unicode.GetString( unicodeBytes );

                if ( group.Id == null )
                {
                    throw new ArgumentNullException( group.ToString(), "group.id is null" );
                }

                if ( group.Name == null )
                {
                    throw new ArgumentNullException( group.ToString(), "unicodeGroupName is null" );
                }

                result.Add( new GroupServiceModel
                {
                    Id = group.Id,
                    Name = unicodeGroupName
                } );
            }

            return result;
        }

        public static LoginServiceModel ToLoginServiceModel( this LoginResponse response )
        {
            var model = new LoginServiceModel
            {
                TenantId = response.TenantId,
                Token = response.Token,
                Login = response.Login,
                Id = response.Id,
                Groups = response.Groups.ToGroupServiceModelList()
            };

            return model;
        }
    }
}
