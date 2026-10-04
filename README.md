# Order tracker

Orderdesk is a shared order-intake workspace. The Angular app provides customer and order CRUD, line items, status tracking, search, and status filters. The .NET API stores the records in PostgreSQL. This first version has no sign-in; all visitors use the same queue.

New orders start in `Pending` and may transition through `Pending` → `Processing` → `Confirmed` → `Completed`. An order may be moved to `Cancelled` from `Pending`, `Processing`, or `Confirmed`; `Completed` and `Cancelled` are terminal states. The API validates each transition, and the UI only offers valid next states.

Order line items include a name, optional SKU, quantity, unit price, and persisted line total. The API computes the line total as quantity × unit price on order create/update. The order total is also stored on the order row as the sum of its line totals, and the `AddOrderTotal` migration backfills it for existing orders.

Prices and totals are displayed as South African rand (ZAR). Each line-item quantity must be a positive whole number within the API's supported integer range.

SKUs are optional and unique within each order, compared after trimming and without regard to letter case. A SKU can be reused on different orders; multiple blank SKU values are allowed.

Customer email addresses are optional but must be unique across customers, compared after trimming and without regard to letter case. Multiple customers may omit an email address.

Customers cannot be deleted while they have orders that are not completed. If all of a customer's orders are completed, deletion preserves those orders with a saved customer-name snapshot and removes the customer reference and contact details.

Order creation rejects a repeat submission for the same customer with the same line items (regardless of line order) during the configured `OrderSubmission:DuplicateWindowSeconds` interval. The default is 10 seconds; set the value to `2` in `stock-api\appsettings.Development.json` while testing, increase it as needed, or set it to `0` to disable duplicate detection. A rejected repeat responds with HTTP 409 and the original order number.

Customers cannot be deleted while they have orders that are not completed. If every linked order is completed, deletion preserves those orders with a saved customer-name snapshot and removes the customer reference and contact details.

## Requirements

- .NET 10 SDK
- Node.js and npm
- A running PostgreSQL server and an existing database for the app

## Run locally

If you do not already have a local settings file, copy the development template. Then set your PostgreSQL password in `stock-api\appsettings.Development.json`:

```powershell
if (-not (Test-Path stock-api\appsettings.Development.json)) {
    Copy-Item stock-api\appsettings.Development.example.json stock-api\appsettings.Development.json
}
```

The local settings file is ignored by Git. Do not commit it or put production credentials in it. The database named in the connection string must already exist. Start the API from the workspace root:

```powershell
dotnet run --project stock-api\stock-api.csproj --launch-profile http
```

The API applies pending EF Core migrations on startup. The initial migration uses `CREATE TABLE IF NOT EXISTS`, so it can adopt the matching tables created by the earlier `EnsureCreated` setup without dropping existing data.

To create a migration after changing the data model:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add DescribeYourChange --project stock-api\stock-api.csproj
```

The API applies new migrations at startup. To apply them explicitly instead, set `ConnectionStrings__OrdersDatabase` in the current PowerShell process and run:

```powershell
dotnet tool run dotnet-ef database update --project stock-api\stock-api.csproj
```

## Run API tests

The API integration tests use a uniquely named, temporary PostgreSQL schema. They do not write to the app's normal schema; the temporary schema is removed when the test run finishes. Set the test connection string from the ignored local development settings, then run:

```powershell
$env:ORDER_API_TEST_CONNECTION_STRING = (Get-Content stock-api\appsettings.Development.json | ConvertFrom-Json).ConnectionStrings.OrdersDatabase
dotnet test stock-api.Tests\stock-api.Tests.csproj
Remove-Item Env:ORDER_API_TEST_CONNECTION_STRING
```

The test database must already exist, and the configured PostgreSQL user must be allowed to create and drop schemas.

In a second terminal, start Angular:

```powershell
Set-Location stock-fe
npm start
```

Open `http://localhost:4200`. The API listens on `http://localhost:5158`; the frontend currently uses this local API address.
