using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using LUC.Interfaces.Exceptions;
using LUC.Interfaces.Helpers;

using NetFwTypeLib;

namespace LUC.Services.Implementation.Helpers
{
    /// http://web.archive.org/web/20081014105153/http://www.dot.net.nz/Default.aspx?tabid=42&mid=404&ctl=Details&ItemID=8

    /// <summary>
    /// Allows basic access to the windows firewall API.
    /// This can be used to add an exception to the windows firewall
    /// exceptions list, so that our programs can continue to run merrily
    /// even when nasty windows firewall is running.
    /// </summary>
    /// 
    ///
    /// Please note: It is not enforced here, but it might be a good idea
    /// to actually prompt the user before messing with their firewall settings,
    /// just as a matter of politeness.
    /// 

    /// 

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
                SingletonInitializer.ThreadSafeInit( 
                    value: () => new FirewallHelper(), 
                    ref s_instance 
                );

                return s_instance;
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

        /// 

        /// Gets whether or not the firewall is installed on this computer.
        /// 

        /// 
        private Boolean IsFirewallInstalled =>
            ( m_fwMgr != null ) &&
            ( m_fwMgr.LocalPolicy != null ) &&
            ( m_fwMgr.LocalPolicy.CurrentProfile != null );

        /// 

        /// Returns whether or not the firewall is enabled.
        /// If the firewall is not installed, this returns false.
        /// 

        public Boolean IsFirewallEnabled => 
            IsFirewallInstalled && 
            m_fwMgr.LocalPolicy.CurrentProfile.FirewallEnabled;

        /// 

        /// Returns whether or not the firewall allows Application "Exceptions".
        /// If the firewall is not installed, this returns false.
        /// 

        /// 
        /// Added to allow access to this metho
        /// 
        private Boolean AppAuthorizationsAllowed => 
            IsFirewallInstalled && 
            !m_fwMgr.LocalPolicy.CurrentProfile.ExceptionsNotAllowed;

        public void GrantAppAuthInAnyNetworksInAllPorts( String applicationFullPath, String appName )
        {
            CheckInputParametersBeforeAuth( appName, applicationFullPath );

            INetFwRule oneRule = NewRuleInAnyNetworks( 
                NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY, 
                description: $"TCP- and UDP-messages exchange", 
                applicationFullPath, 
                appName 
            );

            INetFwPolicy2 firewallPolicy = ComObject<INetFwPolicy2>( "HNetCfg.FwPolicy2" );
            IEnumerable<INetFwRule> allFirewallRules = firewallPolicy.Rules.Cast<INetFwRule>();

            var appRules = new INetFwRule[ 1 ]
            {
                oneRule
            };
            DefineHasAppNecessaryRules( appRules, allFirewallRules, out List<INetFwRule> notAddedRules, out Boolean hasAppNecessaryRules );

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

        private void DefineHasAppNecessaryRules( 
            IEnumerable<INetFwRule> rulesOfApp, 
            IEnumerable<INetFwRule> allRules, 
            out List<INetFwRule> notAddedRules, 
            out Boolean hasAppAuth 
        ){
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

        private INetFwRule NewRuleInAnyNetworks( 
            NET_FW_IP_PROTOCOL_ protocol, 
            String description, 
            String applicationFullPath, 
            String appName, 
            Int32? listeningPort = null 
        ){
            INetFwRule firewallRule = ComObject<INetFwRule>( "HNetCfg.FWRule" );

            firewallRule.Description = description;
            firewallRule.ApplicationName = applicationFullPath;
            firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            firewallRule.Enabled = true;
            firewallRule.InterfaceTypes = "All";//"interfaces" means network interfaces
            firewallRule.Name = appName;
            firewallRule.Protocol = (Int32)protocol;

            if ( listeningPort != null )
            {
                firewallRule.LocalPorts = listeningPort.ToString();
            }

            return firewallRule;
        }

        private TComObject ComObject<TComObject>( String progId )
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
    }
}
