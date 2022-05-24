using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;

using LUC.Interfaces.Exceptions;

using NetFwTypeLib;

namespace LUC.Services.Implementation.Helpers
{
    /// http://web.archive.org/web/20081014105153/http://www.dot.net.nz/Default.aspx?tabid=42&mid=404&ctl=Details&ItemID=8

    /// Allows basic access to the windows firewall API.
    /// This can be used to add an exception to the windows firewall
    /// exceptions list, so that our programs can continue to run merrily
    /// even when nasty windows firewall is running.
    ///
    /// Please note: It is not enforced here, but it might be a good idea
    /// to actually prompt the user before messing with their firewall settings,
    /// just as a matter of politeness.
    /// 

    /// 
    /// To allow the installers to authorize idiom products to work through
    /// the Windows Firewall.
    /// 
    public class FirewallHelper
    {
        #region Variables
        /// 

        /// Hooray! Singleton access.
        /// 

        private static FirewallHelper s_instance = null;

        /// 

        /// Interface to the firewall manager COM object
        /// 

        private readonly INetFwMgr m_fwMgr = null;
        #endregion
        #region Properties
        /// 

        /// Singleton access to the firewallhelper object.
        /// Threadsafe.
        /// 

        public static FirewallHelper Instance
        {
            get
            {
                lock ( typeof( FirewallHelper ) )
                {
                    return s_instance ?? ( s_instance = new FirewallHelper() );
                }
            }
        }
        #endregion
        #region Constructivat0r
        /// 

        /// Private Constructor.  If this fails, HasFirewall will return
        /// false;
        /// 

        private FirewallHelper()
        {
            m_fwMgr = ComObject<INetFwMgr>( progId: "HNetCfg.FwMgr" );
        }

        #endregion

        #region Helper Methods
        /// 

        /// Gets whether or not the firewall is installed on this computer.
        /// 

        /// 
        private Boolean IsFirewallInstalled =>
            m_fwMgr != null &&
            m_fwMgr.LocalPolicy != null &&
            m_fwMgr.LocalPolicy.CurrentProfile != null;

        /// 

        /// Returns whether or not the firewall is enabled.
        /// If the firewall is not installed, this returns false.
        /// 

        public Boolean IsFirewallEnabled => IsFirewallInstalled && m_fwMgr.LocalPolicy.CurrentProfile.FirewallEnabled;

        /// 

        /// Returns whether or not the firewall allows Application "Exceptions".
        /// If the firewall is not installed, this returns false.
        /// 

        /// 
        /// Added to allow access to this metho
        /// 
        private Boolean AppAuthorizationsAllowed => IsFirewallInstalled && !m_fwMgr.LocalPolicy.CurrentProfile.ExceptionsNotAllowed;

        /// 

        /// Adds an application to the list of authorized applications.
        /// If the application is already authorized, does nothing.
        /// 

        /// 
        ///         The full path to the application executable.  This cannot
        ///         be blank, and cannot be a relative path.
        /// 
        /// 
        ///         This is the name of the application, purely for display
        ///         puposes in the Microsoft Security Center.
        /// 
        /// 
        ///         When applicationFullPath is null OR
        ///         When appName is null.
        /// 
        /// 
        ///         When applicationFullPath is blank OR
        ///         When appName is blank OR
        ///         applicationFullPath contains invalid path characters OR
        ///         applicationFullPath is not an absolute path
        /// 
        /// 
        ///         If the firewall is not installed OR
        ///         If the firewall does not allow specific application 'exceptions' OR
        ///         Due to an exception in COM this method could not create the
        ///         necessary COM types
        /// 
        /// 
        ///         If no file exists at the given applicationFullPath
        /// 
        public void GrantAuthInPublicNetwork( String applicationFullPath, String appName )
        {
            CheckInputParametersBeforeAuth( appName, applicationFullPath );

            if ( !HasAuthorization( applicationFullPath ) )
            {
                // Assume failed.
                INetFwAuthorizedApplication appInfo = ComObject<INetFwAuthorizedApplication>( progId: "HNetCfg.FwAuthorizedApplication" );

                appInfo.Name = appName;
                appInfo.ProcessImageFileName = applicationFullPath;
                // ...
                // Use defaults for other properties of the AuthorizedApplication COM object

                // Authorize this application
                m_fwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Add( appInfo );
            }
            // otherwise it already has authorization so do nothing
        }

        public void GrantAppAuthInAnyNetworksInAllPorts( String applicationFullPath, String appName )
        {
            CheckInputParametersBeforeAuth( appName, applicationFullPath );

            INetFwRule oneRule = NewRuleInAnyNetworks( NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY, description: $"TCP- and UDP-messages exchange", applicationFullPath, appName );

            INetFwPolicy2 firewallPolicy = ComObject<INetFwPolicy2>( "HNetCfg.FwPolicy2" );
            IEnumerable<INetFwRule> allFirewallRules = firewallPolicy.Rules.Cast<INetFwRule>();

            var appRules = new INetFwRule[ 1 ] 
            { 
                oneRule
            };
            DefineHasAppNecessaryRules( appRules, allFirewallRules, out List<INetFwRule> notAddedRules, out Boolean hasAppNecessaryRules );

            //for optimization, because rules variable works like stack
            notAddedRules.Reverse();

            if ( !hasAppNecessaryRules )
            {
                foreach ( INetFwRule newRule in notAddedRules )
                {
                    firewallPolicy.Rules.Add( newRule );
                }

                //to be sure that firewallPolicy is updated
                firewallPolicy = ComObject<INetFwPolicy2>( "HNetCfg.FwPolicy2" );
                allFirewallRules = firewallPolicy.Rules.Cast<INetFwRule>();

                DefineHasAppNecessaryRules( appRules, allFirewallRules, out _, out hasAppNecessaryRules );
                if ( !hasAppNecessaryRules )
                {
                    throw new FirewallHelperException( message: "App is not granted after adding it to firewall rules" );
                }
            }
        }

        //public void GrantAppAuthInRules( params INetFwRule[] netFwRules )
        //{

        //}

        private void DefineHasAppNecessaryRules(IEnumerable<INetFwRule> rulesOfApp, IEnumerable<INetFwRule> allRules, out List<INetFwRule> notAddedRules, out Boolean hasAppAuth )
        {
            notAddedRules = rulesOfApp.ToList();

            hasAppAuth = false;
            IEqualityComparer<String> equalityComparer = EqualityComparer<String>.Default;

            foreach ( INetFwRule rule in allRules )
            {
                INetFwRule foundRule = default;

                foreach ( INetFwRule addedRule in notAddedRules )
                {
                    Boolean isSameRule = equalityComparer.Equals( rule.Description, addedRule.Description ) &&
                        equalityComparer.Equals( rule.ApplicationName, addedRule.ApplicationName ) &&
                        rule.Action == addedRule.Action &&
                        rule.Direction == addedRule.Direction &&
                        rule.Enabled == addedRule.Enabled &&
                        equalityComparer.Equals( rule.InterfaceTypes, addedRule.InterfaceTypes ) &&
                        equalityComparer.Equals( rule.Name, addedRule.Name ) &&
                        rule.Protocol == addedRule.Protocol &&
                        equalityComparer.Equals( rule.LocalPorts, addedRule.LocalPorts );
                    if ( isSameRule )
                    {
                        foundRule = addedRule;
                        break;
                    }
                }

                if ( foundRule != default )
                {
                    notAddedRules.Remove( foundRule );

                    if ( notAddedRules.Count == 0 )
                    {
                        hasAppAuth = true;
                        break;
                    }
                }
            }
        }

        private INetFwRule NewRuleInAnyNetworks( NET_FW_IP_PROTOCOL_ protocol, String description, String applicationFullPath, String appName, Int32? listeningPort = null )
        {
            INetFwRule firewallRule = ComObject<INetFwRule>( "HNetCfg.FWRule" );

            firewallRule.Description = description;
            firewallRule.ApplicationName = applicationFullPath;
            firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            firewallRule.Enabled = true;
            firewallRule.InterfaceTypes = "All";//"interfaces" means network interfaces
            firewallRule.Name = appName;
            firewallRule.Protocol = (Int32)protocol;

            if(listeningPort != null)
            {
                firewallRule.LocalPorts = listeningPort.ToString();
            }

            return firewallRule;
        }

        /// Removes an application to the list of authorized applications.
        /// Note that the specified application must exist or a FileNotFound
        /// exception will be thrown.
        /// If the specified application exists but does not current have
        /// authorization, this method will do nothing.
        /// 

        /// 
        ///         The full path to the application executable.  This cannot
        ///         be blank, and cannot be a relative path.
        /// 
        /// 
        ///         When applicationFullPath is null
        /// 
        /// 
        ///         When applicationFullPath is blank OR
        ///         applicationFullPath contains invalid path characters OR
        ///         applicationFullPath is not an absolute path
        /// 
        /// 
        ///         If the firewall is not installed.
        /// 
        /// 
        ///         If the specified application does not exist.
        /// 
        public void RemoveAuthorization( String applicationFullPath )
        {

            #region  Parameter checking
            if ( applicationFullPath == null )
            {
                throw new ArgumentNullException( "applicationFullPath" );
            }

            if ( applicationFullPath.Trim().Length == 0 )
            {
                throw new ArgumentException( "applicationFullPath must not be blank" );
            }

            if ( applicationFullPath.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
            {
                throw new ArgumentException( "applicationFullPath must not contain invalid path characters" );
            }

            if ( !Path.IsPathRooted( applicationFullPath ) )
            {
                throw new ArgumentException( "applicationFullPath is not an absolute path" );
            }

            if ( !File.Exists( applicationFullPath ) )
            {
                throw new FileNotFoundException( "File does not exist", applicationFullPath );
            }
            // State checking
            if ( !IsFirewallInstalled )
            {
                throw new FirewallHelperException( "Cannot remove authorization: Firewall is not installed." );
            }
            #endregion

            if ( HasAuthorization( applicationFullPath ) )
            {
                // Remove Authorization for this application
                m_fwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Remove( applicationFullPath );
            }
            // otherwise it does not have authorization so do nothing
        }
        /// 

        /// Returns whether an application is in the list of authorized applications.
        /// Note if the file does not exist, this throws a FileNotFound exception.
        /// 

        /// 
        ///         The full path to the application executable.  This cannot
        ///         be blank, and cannot be a relative path.
        /// 
        /// 
        ///         The full path to the application executable.  This cannot
        ///         be blank, and cannot be a relative path.
        /// 
        /// 
        ///         When applicationFullPath is null
        /// 
        /// 
        ///         When applicationFullPath is blank OR
        ///         applicationFullPath contains invalid path characters OR
        ///         applicationFullPath is not an absolute path
        /// 
        /// 
        ///         If the firewall is not installed.
        /// 
        /// 
        ///         If the specified application does not exist.
        /// 
        public Boolean HasAuthorization( String applicationFullPath )
        {
            #region  Parameter checking
            if ( applicationFullPath == null )
            {
                throw new ArgumentNullException( "applicationFullPath" );
            }

            if ( applicationFullPath.Trim().Length == 0 )
            {
                throw new ArgumentException( "applicationFullPath must not be blank" );
            }

            if ( applicationFullPath.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
            {
                throw new ArgumentException( "applicationFullPath must not contain invalid path characters" );
            }

            if ( !Path.IsPathRooted( applicationFullPath ) )
            {
                throw new ArgumentException( "applicationFullPath is not an absolute path" );
            }

            if ( !File.Exists( applicationFullPath ) )
            {
                throw new FileNotFoundException( "File does not exist.", applicationFullPath );
            }
            // State checking
            if ( !IsFirewallInstalled )
            {
                throw new FirewallHelperException( "Cannot remove authorization: Firewall is not installed." );
            }

            #endregion

            // Locate Authorization for this application
            foreach ( String appName in GetAuthorizedAppPaths() )
            {
                // Paths on windows file systems are not case sensitive.
                if ( appName.ToLower() == applicationFullPath.ToLower() )
                {
                    return true;
                }
            }

            // Failed to locate the given app.
            return false;

        }

        /// 

        /// Retrieves a collection of paths to applications that are authorized.
        /// 

        /// 
        /// 
        ///         If the Firewall is not installed.
        ///   
        public ICollection GetAuthorizedAppPaths()
        {
            // State checking
            if ( !IsFirewallInstalled )
            {
                throw new FirewallHelperException( "Cannot remove authorization: Firewall is not installed." );
            }

            var list = new ArrayList();
            //  Collect the paths of all authorized applications
            foreach ( INetFwAuthorizedApplication app in m_fwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications )
            {
                list.Add( app.ProcessImageFileName );
            }

            return list;
        }

        private static TComObject ComObject<TComObject>( String progId )
        {
            var authAppType = Type.GetTypeFromProgID( progId, throwOnError: false );

            // Assume failed.
            TComObject comObject = default;

            if ( authAppType != null )
            {
                try
                {
                    comObject = (TComObject)Activator.CreateInstance( authAppType );
                }
                // In all other circumnstances, appInfo is null.
                catch ( ArgumentException ) { }
                catch ( NotSupportedException ) { }
                catch ( System.Reflection.TargetInvocationException ) { }
                catch ( MissingMethodException ) { }
                catch ( MethodAccessException ) { }
                catch ( MemberAccessException ) { }
                catch ( InvalidComObjectException ) { }
                catch ( COMException ) { }
                catch ( TypeLoadException ) { }
            }

            if ( comObject != null )
            {
                return comObject;
            }
            else
            {
                throw new FirewallHelperException( message: $"Can't create {typeof( TComObject )} instance." );
            }
        }

        private void CheckInputParametersBeforeAuth( String appName, String applicationFullPath )
        {
            if ( applicationFullPath == null )
            {
                throw new ArgumentNullException( "applicationFullPath" );
            }

            if ( appName == null )
            {
                throw new ArgumentNullException( "appName" );
            }

            if ( applicationFullPath.Trim().Length == 0 )
            {
                throw new ArgumentException( "applicationFullPath must not be blank" );
            }

            if ( applicationFullPath.Trim().Length == 0 )
            {
                throw new ArgumentException( "appName must not be blank" );
            }

            if ( applicationFullPath.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
            {
                throw new ArgumentException( "applicationFullPath must not contain invalid path characters" );
            }

            if ( !Path.IsPathRooted( applicationFullPath ) )
            {
                throw new ArgumentException( "applicationFullPath is not an absolute path" );
            }

            if ( !File.Exists( applicationFullPath ) )
            {
                throw new FileNotFoundException( "File does not exist", applicationFullPath );
            }
            // State checking
            if ( !IsFirewallInstalled )
            {
                throw new FirewallHelperException( "Cannot grant authorization: Firewall is not installed." );
            }

            if ( !AppAuthorizationsAllowed )
            {
                throw new FirewallHelperException( "Application exemptions are not allowed." );
            }
        }

        #endregion
    }
}
