# Multi-tenancy database design

Prompt: "in entity framework core, how should we model the relationship between Users, UserAccountAccess, and Account? I think User wold contain a collection of UserAccountAccess. UserAccountAccess would contain a reference to its Account. Something else?"

You're on the right track! Here's how I'd model these relationships in Entity Framework Core based on your ADR:

## Entity Models

```csharp
// User entity (extends IdentityUser)
public class ApplicationUser : IdentityUser
{
    public Guid? DefaultAccountId { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserAccountAccess> AccountAccess { get; set; } = new List<UserAccountAccess>();
    public virtual Account? DefaultAccount { get; set; }
}

// Account entity
public class Account : IModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<UserAccountAccess> UserAccess { get; set; } = new List<UserAccountAccess>();
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;
    
    // Financial data collections
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}

// Junction entity for many-to-many with additional properties
public class UserAccountAccess : IModel
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public AccountRole Role { get; set; }
    public string InvitedBy { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Account Account { get; set; } = null!;
    public virtual ApplicationUser InvitedByUser { get; set; } = null!;
}

// Enum for roles
public enum AccountRole
{
    Viewer = 1,
    Editor = 2,
    Owner = 3
}
```

## DbContext Configuration

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // User entity configuration
    modelBuilder.Entity<ApplicationUser>(entity =>
    {
        entity.HasOne(u => u.DefaultAccount)
            .WithMany()
            .HasForeignKey(u => u.DefaultAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    });
    
    // Account entity configuration
    modelBuilder.Entity<Account>(entity =>
    {
        entity.HasKey(a => a.Id);
        
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        
        entity.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);
    });
    
    // UserAccountAccess configuration
    modelBuilder.Entity<UserAccountAccess>(entity =>
    {
        entity.HasKey(uaa => uaa.Id);
        
        // User relationship
        entity.HasOne(uaa => uaa.User)
            .WithMany(u => u.AccountAccess)
            .HasForeignKey(uaa => uaa.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Account relationship
        entity.HasOne(uaa => uaa.Account)
            .WithMany(a => a.UserAccess)
            .HasForeignKey(uaa => uaa.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // InvitedBy relationship
        entity.HasOne(uaa => uaa.InvitedByUser)
            .WithMany()
            .HasForeignKey(uaa => uaa.InvitedBy)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Unique constraint: one user can have only one role per account
        entity.HasIndex(uaa => new { uaa.UserId, uaa.AccountId })
            .IsUnique();
        
        // Convert enum to string in database
        entity.Property(uaa => uaa.Role)
            .HasConversion<string>();
    });
    
    // Financial entities (account-scoped)
    modelBuilder.Entity<Transaction>(entity =>
    {
        entity.HasOne<Account>()
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    });
    
    // Similar configuration for Categories, Budgets, etc.
}
```

## Key Design Decisions

### 1. **Junction Entity with Properties**
Instead of a simple many-to-many, `UserAccountAccess` is a full entity because it has additional properties (Role, JoinedDate, InvitedBy).

### 2. **Navigation Properties**
- `User.AccountAccess` - Collection of all account access records for this user
- `Account.UserAccess` - Collection of all user access records for this account
- Both directions allow easy querying

### 3. **Foreign Key Relationships**
- `UserId` references `AspNetUsers.Id` (string from Identity)
- `AccountId` references `Accounts.Id` (Guid)
- `CreatedBy` and `InvitedBy` reference users

### 4. **Delete Behaviors**
- **Cascade**: When Account is deleted → delete all UserAccountAccess records
- **Cascade**: When User is deleted → delete all their UserAccountAccess records
- **Restrict**: Cannot delete users who created accounts or invited others
- **SetNull**: When DefaultAccount is deleted → set DefaultAccountId to null

## Usage Examples

```csharp
// Get all accounts a user has access to
var userAccounts = await context.Users
    .Where(u => u.Id == userId)
    .SelectMany(u => u.AccountAccess)
    .Include(uaa => uaa.Account)
    .Select(uaa => new { uaa.Account, uaa.Role })
    .ToListAsync();

// Get all users who have access to an account
var accountUsers = await context.Accounts
    .Where(a => a.Id == accountId)
    .SelectMany(a => a.UserAccess)
    .Include(uaa => uaa.User)
    .Select(uaa => new { uaa.User.Email, uaa.Role })
    .ToListAsync();

// Check if user has specific role on account
var hasOwnerAccess = await context.UserAccountAccess
    .AnyAsync(uaa => uaa.UserId == userId && 
                     uaa.AccountId == accountId && 
                     uaa.Role == AccountRole.Owner);
```

This design gives you full flexibility for querying relationships while maintaining referential integrity and supporting your multi-tenant architecture from the ADR.

---

Prompt: "Question about ADR 0009. Is it better to have a separate user preferences table? Or is it better to put the prefs into the Users table? There is 1:1 mapping between user pref rows and user rows."

Great question! For a 1:1 relationship like user preferences, there are good arguments for both approaches. Let me break down the considerations:

## Option 1: Separate UserPreferences Table

```sql
Users (Id, Email, UserName, ...)
UserPreferences (Id, UserId, DefaultAccountId, Theme, DateFormat, Currency, ...)
```

### ✅ Pros:
- **Clean separation** - Core identity data separate from app preferences
- **Optional preferences** - User can exist without preferences row
- **Extensible** - Easy to add new preference columns without touching Identity tables
- **Query optimization** - Can query users without loading preference data
- **Migration friendly** - Adding preferences doesn't require altering Identity tables

### ❌ Cons:
- **JOIN overhead** - Need JOIN to get user + preferences
- **Potential orphan data** - Preferences could exist without user (if not careful)
- **More complex queries** - `LEFT JOIN` needed since preferences might not exist

## Option 2: Preferences in Users Table

```sql
Users (Id, Email, UserName, DefaultAccountId, Theme, DateFormat, Currency, ...)
```

### ✅ Pros:
- **Simple queries** - Everything in one table
- **Better performance** - No JOINs needed
- **Guaranteed consistency** - Preferences always exist with user
- **Easier EF modeling** - Just properties on ApplicationUser

### ❌ Cons:
- **Mixed concerns** - Identity data mixed with app-specific data
- **Migration complexity** - Adding preferences requires altering Identity table
- **Nullable columns** - Preferences need default values or nullable types

## My Recommendation: **Separate UserPreferences Table**

For your YoFi.V3 project, I'd recommend the separate table approach because:

### 1. **You're Extending IdentityUser**
```csharp
public class ApplicationUser : IdentityUser
{
    // Keep this clean - just navigation properties
    public virtual UserPreferences? Preferences { get; set; }
    public virtual ICollection<UserAccountAccess> AccountAccess { get; set; }
}

public class UserPreferences
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? DefaultAccountId { get; set; }
    public string Theme { get; set; } = "light";
    
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Account? DefaultAccount { get; set; }
}
```

### 2. **Future Flexibility**
Your ADR mentions starting with minimal preferences but expanding later. A separate table makes this much easier:

```csharp
// Easy to add later without touching Identity
public class UserPreferences
{
    // Phase 1: Minimal
    public Guid? DefaultAccountId { get; set; }
    public string Theme { get; set; } = "light";
    
    // Phase 2: Add these later
    // public string DateFormat { get; set; }
    // public string Currency { get; set; }
    // public bool EmailNotifications { get; set; }
    // public TimeZoneInfo TimeZone { get; set; }
}
```

### 3. **EF Configuration**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // User preferences (1:1 optional)
    modelBuilder.Entity<ApplicationUser>()
        .HasOne(u => u.Preferences)
        .WithOne(p => p.User)
        .HasForeignKey<UserPreferences>(p => p.UserId)
        .OnDelete(DeleteBehavior.Cascade);
        
    // Default account relationship
    modelBuilder.Entity<UserPreferences>()
        .HasOne(p => p.DefaultAccount)
        .WithMany()
        .HasForeignKey(p => p.DefaultAccountId)
        .OnDelete(DeleteBehavior.SetNull);
}
```

### 4. **Usage Pattern**
```csharp
// Get user with preferences
var user = await context.Users
    .Include(u => u.Preferences)
    .Include(u => u.AccountAccess)
    .FirstAsync(u => u.Id == userId);

// Create preferences if they don't exist
if (user.Preferences == null)
{
    user.Preferences = new UserPreferences 
    { 
        UserId = user.Id,
        Theme = "light"
    };
    await context.SaveChangesAsync();
}
```

## Implementation Tips

### 1. **Lazy Creation Pattern**
```csharp
public async Task<UserPreferences> GetOrCreateUserPreferencesAsync(string userId)
{
    var prefs = await context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
    if (prefs == null)
    {
        prefs = new UserPreferences { UserId = userId };
        context.UserPreferences.Add(prefs);
        await context.SaveChangesAsync();
    }
    return prefs;
}
```

### 2. **Extension Method for Convenience**
```csharp
public static class ApplicationUserExtensions
{
    public static async Task<UserPreferences> GetPreferencesAsync(
        this ApplicationUser user, 
        ApplicationDbContext context)
    {
        if (user.Preferences != null) return user.Preferences;
        
        return await context.GetOrCreateUserPreferencesAsync(user.Id);
    }
}
```

## Updated ADR Schema

```sql
-- Users (from ASP.NET Core Identity)
Users (Id, Email, UserName)

-- Account entity
Accounts (Id, Key, Name, IsActive)

-- User-to-Account relationship with roles  
UserAccountAccess (Id, UserId, AccountId, Role)

-- User preferences (separate table for flexibility)
UserPreferences (Id, UserId, DefaultAccountId, Theme)

-- All financial data is account-scoped
Transactions (Id, AccountId, Date, Amount, Description, ...)
```

The separate table approach gives you the best balance of clean separation, future flexibility, and performance for your multi-tenant architecture.