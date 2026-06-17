// CreditAccount entity retired — P10-13 DropLegacyCreditAccounts migration.
// This file is intentionally left empty. The CreditAccount entity is no longer registered
// with EF Core (no DbSet, no IEntityTypeConfiguration). The domain class (CreditAccount.cs)
// is retained only as a historical reference; it is not tracked by EF.
//
// The billing.CreditAccounts table is dropped by the DropLegacyCreditAccounts migration.
// CreditTransactions.CreditAccountId remains as a plain nullable int (historical marker).
