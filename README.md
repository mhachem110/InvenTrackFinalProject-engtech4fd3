# InvenTrack (ENGTECH 4FD3) — Inventory Management Platform

InvenTrack is a web-based inventory management system built with **ASP.NET Core MVC**, **Entity Framework Core**, and **Azure SQL**.  
It supports inventory items (with photos), categories, storage locations, **stock transactions (check-in / check-out / adjustment / transfers with per-location stock)**, and **role-based access control** using ASP.NET Core Identity.

---

## Features

### Inventory
- Create, view, edit, delete inventory items
- Unique SKU enforcement
- Item photo + thumbnail upload (WebP processing)
- Reorder level tracking + low stock badges

### Categories & Locations
- Full CRUD for Categories and Storage Locations
- Inventory items reference Category and a primary Location

### Stock Transactions
- **Check In** (add stock to a location)
- **Check Out** (remove stock from a location)
- **Adjustment** (increase/decrease stock in a location; notes required)
- **Transfer** (move partial quantity between locations)
- Transactions history shown per item and globally

### Per-Location Stock
- Tracks quantities per location using `InventoryItemStock`
- Automatically recalculates total `QuantityOnHand` per item after every transaction
- Automatically selects a “primary” location (highest stock)

### Authentication & Authorization (Identity)
- Email/password login + register
- Email confirmation required
- Roles: **Admin**, **Manager**, **Viewer**
- Admin user management page (create users, assign roles, reset password, lock/unlock, delete users)

### Email Notifications
- **Email confirmation** on registration
- **Reorder alerts** emailed to Admins & Managers when stock drops to/below reorder level
- Email sender supports **SendGrid** (recommended). SMTP can be enabled optionally.

---

## Tech Stack
- ASP.NET Core MVC + Razor Pages (Identity UI)
- EF Core + Azure SQL
- Bootstrap 5 + Custom theme (`it-*` classes)
- SendGrid email delivery

---

## Getting Started

### Prerequisites
- Visual Studio 2022/2026 (or `dotnet` CLI)
- .NET SDK installed
- Azure SQL connection strings
- SendGrid API key (recommended)

---

## Configuration

### 1) Connection Strings
Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "IdentityContext": "YOUR_AZURE_SQL_CONN_FOR_IDENTITY",
    "InvenTrackContext": "YOUR_AZURE_SQL_CONN_FOR_APP_DATA"
  }
}
