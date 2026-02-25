# InvenTrack

InvenTrack is a web application for tracking inventory items, where they are stored, how much stock is available, and how stock changes over time. The system is designed for small teams that need a clear view of items, categories, locations, and a reliable transaction history for audits and daily operations.

The app focuses on two goals:
1. Keeping inventory data organized and easy to browse
2. Recording every stock change as a transaction so updates are traceable

## What the app does

### Inventory items
Users can create and manage inventory items with:
- Item name, SKU, description
- Quantity on hand and reorder level
- Active or inactive status
- Category and a primary storage location
- Optional photo and thumbnail to help identify items quickly

### Categories and storage locations
The app includes:
- Categories to group items for consistent naming and reporting
- Storage locations to represent physical areas such as rooms, shelves, or buildings

### Stock transactions and history
Instead of manually editing quantities, stock is updated through transactions. This keeps a clear history of what happened and when.

Supported transaction types:
- Check In: adds stock to a selected location
- Check Out: removes stock from a selected location
- Adjustment: corrects stock up or down in a selected location and requires notes
- Transfer: moves partial quantities between locations

The app maintains per location stock using a dedicated stock table and automatically recalculates the item total quantity on hand after each transaction. A transaction history is available per item and also as a global list.

### Low stock prediction and reorder suggestions
InvenTrack is designed to use the transaction history as usage data. A prediction module will analyze historical inventory changes and estimate low stock risk before items reach a critical threshold. The goal is to show:
- A risk label such as Reorder Soon or Reorder Now
- A suggested reorder quantity based on recent usage trends
- A clear Insufficient Data state when there is not enough history to make a useful estimate

This feature is intended as decision support, not a guarantee.

### Authentication and roles
Access is controlled through user accounts and roles:
- Admin: full access, including user administration
- Manager: can manage inventory and transactions
- Viewer: read only access

Admins can manage users from the UI, including role assignment, password resets, lock and unlock, and permanent user deletion.

### Alerts and emails
The app supports:
- Email confirmation during registration
- Reorder alerts sent to Admin and Manager accounts when stock drops to the reorder threshold

## Current stage

### Completed
- Inventory CRUD with validation and image upload
- Category and location CRUD
- Stock transaction system with per location stock tracking
- Partial transfers between locations
- Transaction history views
- Role based access control for core modules
- Admin user management UI
- Email confirmation and reorder alert wiring with SendGrid support
- UI theme applied across the main application and key Identity pages

### In progress
- Finishing theme updates for remaining Identity pages
- Finalizing alert reliability and ensuring email sender setup is consistent across environments
- Polishing navigation and page layout consistency across modules

### Upcoming implementations
- Low stock prediction feature using transaction history, including risk labels and suggested reorder quantities
- Inventory and transaction reporting views, including summary filters and movement views
- Export features for inventory and transaction logs
- Search and filtering improvements across transactions and item lists
- Additional account security polish and admin audit views

## Team
Built by Group 8 for ENGTECH 4FD3.
