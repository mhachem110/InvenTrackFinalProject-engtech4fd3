# InvenTrack

InvenTrack is a web application for tracking inventory items, stock levels, storage locations, stock movement, and transfer requests across locations. It is designed for internal teams that need a clear view of what they have, where it is stored, and how inventory changes over time.

The system is built around three main ideas:
1. Keeping inventory organized and easy to browse
2. Recording stock movement through structured transactions
3. Controlling access by user role and assigned location

## What the app does

### Inventory items
Users can create, view, and manage inventory items with:
- Item name, SKU, and description
- Quantity on hand and reorder level
- Active or inactive status
- Category and primary storage location
- Optional photo and thumbnail for item identification

The system also supports per-location stock tracking, so an item can exist across more than one storage location while still maintaining a total quantity on hand.

### Categories and storage locations
The app includes:
- Categories for grouping and organizing inventory items
- Storage locations for physical inventory areas such as rooms, labs, offices, or storage areas

These help keep inventory structured and make reporting and filtering easier.

### Stock transactions and history
Stock is managed through transactions rather than direct quantity edits. This keeps inventory changes traceable and preserves an audit trail.

Supported transaction types:
- Check In: add stock to a selected location
- Check Out: remove stock from a selected location
- Adjustment: correct stock up or down in a selected location, with notes
- Transfer: move stock between locations

The system maintains per-location stock using a dedicated stock table and updates the item total quantity automatically after each transaction. Users can view transaction history globally and per item.

### Transfer requests
InvenTrack includes a transfer request workflow for location-based operations.

Users can submit transfer requests when stock needs to move from one location to another. Depending on role:
- Some users can request transfers only
- Some users can review and approve or reject requests
- Approved requests are applied through the transaction system

The app also supports transfer request badges and notifications so pending requests are easier to notice and review.

### Reports and exports
The app includes reporting views to support demos, reviews, and operational checks.

Available reporting areas include:
- Low stock report
- Location stock summary
- Recent transactions report

The system also supports CSV export for inventory and report data.

## Authentication, roles, and access levels

Access is controlled through user accounts, roles, and assigned locations.

### Roles
- **Admin**: full system access, including user administration and all locations
- **Regional Manager**: broad operational access across all locations, but no user administration
- **Manager**: operational access for an assigned location
- **Supervisor**: can manage most inventory work for an assigned location, but cannot confirm stock transfers between locations
- **Employee**: read access for an assigned location and can request transfers, but cannot edit or delete inventory records

### Location-based visibility
Scoped users only see data for their assigned location. This affects:
- Inventory visibility
- Transactions
- Transfer requests
- Location-based operational views

Admins can reassign users to different locations when responsibilities change.

### User administration
Admins can manage users from the UI, including:
- Creating accounts
- Assigning roles
- Assigning or changing locations
- Resetting passwords
- Locking and unlocking accounts
- Deleting accounts

User onboarding is handled by the admin workflow rather than open self-registration.

## Current stage

### Completed
- Inventory CRUD with validation and image upload
- Category CRUD
- Storage location CRUD
- Stock transaction system with per-location stock tracking
- Partial stock transfers between locations
- Item details pages with stock and history views
- Pagination across major list pages
- Reporting views for low stock, location summary, and recent transactions
- CSV export for reporting and inventory data
- Role-based access control
- Location-scoped access control
- Expanded user roles and admin user management
- Transfer request workflow
- Transfer request badges and notification support
- Internal account setup flow managed by admins
- Updated UI theme across main application pages

### In progress
- Refining the new access control structure across all views and controller paths
- Polishing transfer request notifications and visibility rules
- Finalizing consistency across scoped dashboards and role-specific pages
- Continued cleanup of Identity and admin experience

### Upcoming implementations
- Additional dashboard improvements by role
- More detailed reporting and filtering options
- Stronger audit and operational review views
- Additional notification polish and reliability improvements
- Security and admin usability improvements
- Optional future decision-support features based on inventory history

## Team
Built by Group 8 for ENGTECH 4FD3.
