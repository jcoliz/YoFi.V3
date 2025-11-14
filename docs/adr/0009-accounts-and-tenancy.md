# 0009. Multi-tenancy and account model

Date: 2025-11-13

## Status

Accepted

## Context

### Question

How should YoFi.V3 handle multiple users and financial data isolation? What is the relationship between users and financial "accounts"?

### Background

YoFi.V3 is a rewrite of the [YoFi personal finance application](https://github.com/jcoliz/yofi). YoFi is single-tenant. This is a constraint I would like to improve
upon with this rewrite.

### Key Questions

1. **What is an "Account"?**
   - A financial institution account (bank account, credit card)?
   - A logical grouping of financial data (household, business)?
   - A tenant boundary for multi-user access?

2. **User-to-Account Relationships:**
   - Can one user access multiple accounts?
   - Can multiple users access the same account?
   - How are permissions managed?

3. **Data Isolation:**
   - How is financial data segregated?
   - What happens when a user is removed from an account?

### Use Case Analysis

**Primary Use Cases:**
- **Personal Finance**: Individual manages their own financial data
- **Household Finance**: Family members share access to household financial data
- **Small Business**: Business owner + bookkeeper access business financial data
- **Multi-Account**: User manages both personal and business finances separately

**Secondary Use Cases:**
- **Financial Advisor**: Advisor has read-only access to client accounts
- **Shared Expenses**: Roommates track shared expenses
- **Family Financial Planning**: Parents and adult children coordinate finances

## Decision

### Account Model: **Logical Financial Boundary**

An "Account" in YoFi.V3 represents a **logical boundary for financial data** - not a bank account, but a complete set of financial records (transactions, budgets, categories, etc.) managed as a unit.

Examples:
- "Smith Family Finances" (household account)
- "John's Personal Finances" (individual account)
- "ABC Consulting Business" (business account)

As an example, I would like to have my own personal "Account" which only I can access, as well as an "Account" that my wife and I share (household account).

"Accounts" in YoFi have a one-to-many relationship with accounts at a bank. As it stands today, I download all transactions from multiple credit cards, and our
savings account, and our checking account into a single YoFi "Account". Generally this
works well, however, it would be useful to track which bank account any transaction
came from.

### Multi-Tenancy Model: **Multi-Account Users with Role-Based Access**

#### User-to-Account Relationships

**One User → Multiple Accounts**: Users can have access to multiple accounts
- Personal account + business account
- Multiple business accounts
- Family account + personal account

**Multiple Users → One Account**: Accounts can have multiple users with different roles
- Family members accessing household finances
- Business owner + bookkeeper
- Financial advisor with read-only access

#### Permission Model

**Three Role Levels:**
1. **Owner** - Full control (edit data, manage users, delete account)
2. **Editor** - Edit financial data, view reports (cannot manage users)
3. **Viewer** - Read-only access to data and reports

**Account Management:**
- Account creation automatically makes creator the Owner
- Only Owners can invite/remove users
- Only Owners can change user roles
- Each account must have at least one Owner

#### Data Isolation

**Complete Separation**: Financial data is completely isolated by account
- Transactions, categories, budgets are account-scoped
- No cross-account data sharing
- User preferences are global (not account-scoped)

#### Database Schema Implications

```sql
-- Users (from ASP.NET Core Identity)
Users (Id, Email, UserName)

-- Account entity
Accounts (Id, Name, CreatedBy, CreatedDate, IsActive)

-- User-to-Account relationship with roles
UserAccountAccess (UserId, AccountId, Role, InvitedBy, JoinedDate)

-- All financial data is account-scoped
Transactions (Id, AccountId, Date, Amount, Description, ...)
Categories (Id, AccountId, Name, ...)
Budgets (Id, AccountId, Month, Amount, ...)

-- User preferences (start global)
UserPreferences (UserId, DefaultAccountId, Theme, ...)
```

### Implementation Details

#### JWT Claims Structure

```json
{
  "sub": "user123",
  "email": "john@example.com",
  "entitlements": "account1_guid:owner,account2_guid:editor,account3_guid:viewer"
}
```

#### API URL Structure

```
/api/account/{AccountID}/transactions
/api/account/{AccountID}/categories
/api/account/{AccountID}/budgets
/api/account/{AccountID}/reports
```

#### Authorization Policies

- **AccountView**: User must have Viewer, Editor, or Owner role for the account
- **AccountEdit**: User must have Editor or Owner role for the account
- **AccountOwn**: User must have Owner role for the account

### Account Lifecycle

#### User Invitation
1. Owner sends invitation by email, choosing the invited role
2. Invited user accepts → gets specified role
3. Email notifications for account activity

#### Account Creation
1. **First User/Admin**: Can register directly and gets a personal account
2. **Subsequent Users**: Must be invited by existing account owners
3. Upon registration → Auto-create personal account for the new user
4. User logs in → Redirect to their default account dashboard  
5. User can switch accounts via "Accounts" page

#### User Removal
1. Only Owners can remove users
2. Owners cannot remove themselves if they're the last Owner
3. User data (preferences) remains, but account access is revoked

#### Account Deletion
1. Only possible if user is the sole Owner
2. All financial data is permanently deleted
3. Other users lose access immediately

## Consequences

### What becomes easier:
- **Clear Data Boundaries**: Complete separation prevents data leaks
- **Flexible Use Cases**: Supports personal, household, and business scenarios
- **Scalable Authorization**: Role-based access scales to complex scenarios
- **Family-Friendly**: Multiple family members can collaborate
- **Business-Ready**: Proper permission model for business use

### What becomes more complex:
- **Database Queries**: All queries must be account-scoped
- **User Experience**: Users need to select/switch between accounts
- **Invitation Flow**: Need email invitations and acceptance workflow
- **Error Handling**: Need to handle account access denied scenarios
- **Account Management UI**: Need account settings, user management pages

### Migration Impact:
- **From YoFi V1/V2**: Existing user data becomes their personal account
- **New Features**: All new features must be account-aware from day one
- **API Design**: All endpoints must include account context

### Technical Implications:
- **Application Layer**: All Features must accept AccountId parameter
- **Controllers**: Account-scoped authorization on all endpoints  
- **Frontend**: Account selection/switching UI component
- **Database**: Account foreign keys on all business entities

## Implementation Phases

### Phase 1: Single-User Accounts (MVP)
- Full database schema implemented
- User invitation UI disabled (accounts are single-user by default)
- Account creation during user registration
- All financial features account-scoped

### Phase 2: Multi-User Accounts
- User invitation system
- Role-based permissions
- Account management UI

### Phase 3: Advanced Features
- Account transfer/ownership change
- Account archival/soft delete
- Audit logging for account access

## Related Decisions

- [ADR 0008: Identity System](0008-identity.md) - Provides the authentication foundation for this account model
- [ADR 0005: Database Backend](0005-database-backend.md) - SQLite database will store account-scoped data

## Migration from current YoFi

All existing YoFi data (transactions, categories, budgets, etc.) will be migrated to a single account that will be identified during the migration process. This ensures existing users can continue using their historical data within the new multi-account structure.

## Questions for Review

1. **Is the three-role model sufficient?** (Owner/Editor/Viewer) 
   ✅ **Yes, this is perfect.**

2. **Should user preferences be account-scoped or global?** 
   ✅ **Start with global preferences** for simplicity. There isn't an obvious need for preferences right now. Just to have something here, we could save light/dark mode theme switch.

3. **How should account switching work in the UI?** 
   ✅ **Account selection page approach**: User visits "Accounts" page and selects current account. This context applies to all subsequent UI actions. Consider adding account switcher in navigation for quick switching.

4. **Should there be a default account concept for new users?** 
   ✅ **Yes, auto-provision personal account**: New users automatically get a personal account they own, eliminating empty state.

5. **How do we handle users with no account access (edge case)?** 
   ✅ **Enable account creation**: On empty "Accounts" page, allow user to create new account.

## Assorted Technical Details

- **Account IDs**: Use GUIDs for security and uniqueness. My technical policy is that all identifiers which are visible to users are GUIDs.
- **Default Account**: Updated when user switches accounts. If deault account is not accessible (user lost access, or account deleted), then user will be redirected to account switching page to choose a new default account.
