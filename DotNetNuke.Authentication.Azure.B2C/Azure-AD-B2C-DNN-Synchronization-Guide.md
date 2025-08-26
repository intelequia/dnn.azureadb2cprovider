# Azure AD B2C and DNN Synchronization Guide

## Overview

This document explains how user profile data and role assignments are synchronized between Azure Active Directory B2C (Azure AD B2C) and DNN (DotNetNuke) Platform. The synchronization occurs through two primary mechanisms: **real-time synchronization during login** and **scheduled background synchronization**.

## Synchronization Architecture

### Configuration Settings

The synchronization behavior is controlled by several configuration settings in `AzureConfig`:

- **`RoleSyncEnabled`**: Enables/disables role synchronization
- **`UserSyncEnabled`**: Enables/disables user synchronization via scheduled tasks
- **`ProfileSyncEnabled`**: Enables/disables profile data synchronization
- **`ExpiredRolesSyncEnabled`**: Enables/disables cleanup of expired role assignments
- **`UseGlobalSettings`**: Determines if portal-specific or global settings are used

## Real-Time Synchronization (During Login)

### When It Happens
Real-time synchronization occurs every time a user successfully authenticates with Azure AD B2C and logs into DNN.

### User Profile Synchronization

#### Basic Profile Data
During login, the following user profile data is synchronized from Azure AD B2C JWT token claims to DNN:

| Azure AD B2C Claim | DNN Profile Property | Notes |
|-------------------|---------------------|-------|
| `emails` | Email | Primary email address |
| `given_name` | FirstName | User's first name |
| `family_name` | LastName | User's last name |
| `name` | DisplayName | User's display name |
| `city` | City | User's city |
| `country` | Country | User's country |
| `postalCode` | PostalCode | User's postal code |
| `state` | Region | User's state/region |
| `streetAddress` | Street | User's street address |

#### Custom Profile Properties
Custom claims from Azure AD B2C can be mapped to DNN profile properties using **Profile Mappings**:
- Custom claims are identified by prefix (e.g., `extension_<app-id>_customAttribute`)
- Mapped to specific DNN profile properties via configuration
- Only synchronized if `ProfileSyncEnabled` is true

#### Profile Picture Synchronization
- User profile pictures are retrieved from Microsoft Graph API
- Pictures are stored in the user's DNN file folder
- File format is automatically detected (PNG, JPG, GIF, BMP)
- Only occurs if `ProfileSyncEnabled` is true

### Role Synchronization

#### How Roles Are Synchronized
1. **Group Retrieval**: System fetches user's group memberships from Azure AD B2C via Microsoft Graph API
2. **Role Mapping**: Azure AD B2C groups are mapped to DNN roles using either:
   - **Direct mapping**: Azure AD B2C group name becomes DNN role name (with optional prefix)
   - **Custom mapping**: Configured mappings between Azure AD B2C groups and DNN roles
3. **Role Assignment**: User is added to corresponding DNN roles
4. **Role Removal**: User is removed from DNN roles they no longer belong to in Azure AD B2C

#### Role Naming Convention
- **With Prefix**: `Azure-B2C-{GroupName}` (if `GroupNamePrefixEnabled` is true)
- **Without Prefix**: `{GroupName}` (if `GroupNamePrefixEnabled` is false)
- **Custom Mapping**: Uses configured DNN role name regardless of prefix settings

#### Role Groups
- Roles can be organized into role groups using the `[DNNRoleGroup={GroupName}]` format in the Azure AD B2C group description
- Role groups are automatically created if they don't exist

### User Creation Process
If a user doesn't exist in DNN:
1. Create new DNN user account
2. Set random password (user authenticates via Azure AD B2C)
3. Map Azure AD B2C user attributes to DNN user properties
4. Add user to appropriate roles based on group memberships
5. Mark user with `IdentitySource = "Azure-B2C"` profile property

## Scheduled Background Synchronization

### When It Happens
Background synchronization runs as a scheduled task in DNN, typically configured to run periodically (e.g., daily, hourly).

### Synchronization Types

#### 1. Role Synchronization (`SyncRoles`)
- **Purpose**: Ensures DNN roles stay in sync with Azure AD B2C groups
- **Process**:
  - Retrieves all groups from Azure AD B2C
  - Creates missing DNN roles for Azure AD B2C groups
  - Removes DNN roles that no longer exist in Azure AD B2C
  - Updates role descriptions and role groups

#### 2. User Synchronization (`SyncUsers`)
- **Purpose**: Ensures all Azure AD B2C users exist in DNN with current profile data
- **Process**:
  - Retrieves all users from Azure AD B2C
  - Creates missing DNN users for Azure AD B2C users
  - Updates existing DNN users with latest profile data
  - Syncs profile pictures if `ProfileSyncEnabled` is true

#### 3. Expired Role Cleanup (`SyncExpiredRoles`)
- **Purpose**: Removes users from Azure AD B2C groups when their DNN role assignments have expired
- **Process**:
  - Identifies DNN users with expired role assignments
  - Removes these users from corresponding Azure AD B2C groups
  - Maintains consistency between DNN and Azure AD B2C

### Scheduled Task Configuration
The scheduled task iterates through all DNN portals and synchronizes each enabled portal:

```csharp
foreach (var portal in portals)
{
    var settings = new AzureConfig(AzureConfig.ServiceName, portal.PortalID);
    if (settings.Enabled)
    {
        if (settings.RoleSyncEnabled)
            SyncRoles(portal.PortalID, settings);
        
        if (settings.UserSyncEnabled)
            SyncUsers(portal.PortalID, settings);
        
        if (settings.ExpiredRolesSyncEnabled)
            SyncExpiredRoles(portal.PortalID, settings);
    }
}
```

## Data Mapping and Customization

### User Property Mappings
User mappings define how Azure AD B2C user attributes map to DNN user properties:

| DNN Property | Default B2C Claim | Configurable |
|--------------|-------------------|--------------|
| Id | `sub` | Yes |
| FirstName | `given_name` | Yes |
| LastName | `family_name` | Yes |
| DisplayName | `name` | Yes |
| Email | `emails` | Yes |
| PortalId | N/A | Yes (for multi-portal scenarios) |

### Role Mappings
Role mappings allow custom relationships between Azure AD B2C groups and DNN roles:

| Azure AD B2C Group | DNN Role | Notes |
|-------------------|----------|-------|
| `Administrators` | `Portal Administrators` | Custom mapping |
| `ContentEditors` | `Content Editors` | Custom mapping |
| `Members` | `Registered Users` | Custom mapping |

### Profile Mappings
Profile mappings enable synchronization of custom attributes:

| Azure AD B2C Custom Attribute | DNN Profile Property |
|------------------------------|---------------------|
| `extension_abc123_Department` | `Department` |
| `extension_abc123_JobTitle` | `JobTitle` |
| `extension_abc123_Manager` | `Manager` |

## Sync Frequency and Timing

### Real-Time Sync
- **Trigger**: Every user login
- **Latency**: Immediate
- **Scope**: Current user only
- **Data**: Profile data, roles, profile picture

### Scheduled Sync
- **Trigger**: Configured schedule (e.g., daily at 2 AM)
- **Latency**: Up to schedule interval
- **Scope**: All users and roles
- **Data**: All users, all roles, expired role cleanup

## Error Handling and Logging

### Sync Errors
- Errors are logged with detailed messages
- Sync continues for other users/roles even if individual items fail
- Error statistics are tracked and reported

### Logging Output Example
```
Portal 0 synchronized successfully (sync errors: 0; groups created: 2; groups deleted: 0).
Portal 0 synchronized successfully (sync errors: 0; users created: 5; users updated: 23).
```

## Best Practices

### Configuration Recommendations
1. **Enable Profile Sync**: Set `ProfileSyncEnabled = true` for complete user data synchronization
2. **Use Role Mappings**: Define custom role mappings for better role name control
3. **Schedule Appropriately**: Run scheduled sync during low-traffic hours
4. **Monitor Logs**: Regularly review sync logs for errors

### Performance Considerations
1. **Large User Bases**: Scheduled sync may take time with thousands of users
2. **Graph API Limits**: Be aware of Microsoft Graph API throttling limits
3. **Network Latency**: Sync performance depends on network connectivity to Azure

### Security Considerations
1. **Service Principal**: Use dedicated service principal with minimal required permissions
2. **Credential Management**: Store AAD Application ID and Key securely
3. **Audit Logging**: Enable logging for security audit purposes

## Extensibility

### Custom Sync Procedures
- **Pre-sync**: `{ServiceName}_BeforeScheduledSync` stored procedure
- **Post-sync**: `{ServiceName}_AfterScheduledSync` stored procedure

### Login Validation
- Implement `ILoginValidation` interface for custom authentication logic
- Configure via `AzureADB2C.LoginValidationProvider` app setting

### Filtering
- Use `AzureADB2C.GetUserGroups.Filter` app setting to limit synchronized groups
- Define group prefixes separated by semicolons (e.g., `DNN_;APP_`)

## Troubleshooting

### Common Issues
1. **Missing Profile Data**: Check if required claims are included in Azure AD B2C user flow
2. **Role Sync Failures**: Verify service principal has proper permissions to read groups
3. **User Not Created**: Check if user mapping configuration is correct
4. **Profile Picture Issues**: Ensure user has profile picture set in Azure AD B2C

### Diagnostic Steps
1. Check DNN event logs for detailed error messages
2. Verify Azure AD B2C application configuration
3. Test Graph API connectivity with service principal credentials
4. Review user flow configuration for required claims

## Conclusion

The Azure AD B2C DNN authentication provider offers comprehensive synchronization capabilities that ensure user data consistency between Azure AD B2C and DNN. By combining real-time login synchronization with scheduled background tasks, the system maintains up-to-date user profiles and role assignments while providing flexibility for customization and scalability.