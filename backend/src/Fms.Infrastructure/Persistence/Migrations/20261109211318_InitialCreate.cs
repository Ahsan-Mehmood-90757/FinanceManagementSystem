using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_sequences", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "expense_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "financial_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    account_number = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    opening_balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    allow_negative = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    phone = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    email = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tax_number = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    opening_balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    phone = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    email = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tax_number = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    invoice_footer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    go_live_date = table.Column<DateOnly>(type: "date", nullable: false),
                    setup_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    setup_completed_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    symbol = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    allows_fractions = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    amount_in = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    amount_out = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_ledger_entries_financial_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "financial_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    debit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_party_ledger_entries_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    charges_on_party = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    charges_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    void_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_bills", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_bills_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_of_goods = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    charges_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    void_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_bills", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_bills_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expense_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    linked_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_document_number = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    void_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_vouchers_expense_categories_expense_category_id",
                        column: x => x.expense_category_id,
                        principalTable: "expense_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vouchers_financial_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "financial_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vouchers_financial_accounts_to_account_id",
                        column: x => x.to_account_id,
                        principalTable: "financial_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vouchers_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_sale_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reorder_level = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tracks_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    purchase_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_returns", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_returns_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_returns_purchase_bills_purchase_bill_id",
                        column: x => x.purchase_bill_id,
                        principalTable: "purchase_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sale_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expense_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    paid_from_account_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bill_charges", x => x.id);
                    table.CheckConstraint("ck_bill_charges_one_parent", "(purchase_bill_id IS NULL) <> (sale_bill_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_bill_charges_expense_categories_expense_category_id",
                        column: x => x.expense_category_id,
                        principalTable: "expense_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_charges_financial_accounts_paid_from_account_id",
                        column: x => x.paid_from_account_id,
                        principalTable: "financial_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_charges_purchase_bills_purchase_bill_id",
                        column: x => x.purchase_bill_id,
                        principalTable: "purchase_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bill_charges_sale_bills_sale_bill_id",
                        column: x => x.sale_bill_id,
                        principalTable: "sale_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    sale_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_returns", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_returns_parties_party_id",
                        column: x => x.party_id,
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_returns_sale_bills_sale_bill_id",
                        column: x => x.sale_bill_id,
                        principalTable: "sale_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    original_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_batches_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_bill_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_bill_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_bill_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_bill_lines_sale_bills_sale_bill_id",
                        column: x => x.sale_bill_id,
                        principalTable: "sale_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity_in = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    quantity_out = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_movements_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_movements_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_bill_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_bill_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_bill_lines_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_bill_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_bill_lines_purchase_bills_purchase_bill_id",
                        column: x => x.purchase_bill_id,
                        principalTable: "purchase_bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_adjustments_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_adjustments_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_line_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_line_allocations", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_line_allocations_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_line_allocations_sale_bill_lines_sale_bill_line_id",
                        column: x => x.sale_bill_line_id,
                        principalTable: "sale_bill_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_return_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_reversed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_return_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_return_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_return_lines_sale_bill_lines_sale_bill_line_id",
                        column: x => x.sale_bill_line_id,
                        principalTable: "sale_bill_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_return_lines_sale_returns_sale_return_id",
                        column: x => x.sale_return_id,
                        principalTable: "sale_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_return_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_return_lines_inventory_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_return_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_return_lines_purchase_bill_lines_purchase_bill_lin~",
                        column: x => x.purchase_bill_line_id,
                        principalTable: "purchase_bill_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_return_lines_purchase_returns_purchase_return_id",
                        column: x => x.purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_ledger_entries_account_id_date",
                table: "account_ledger_entries",
                columns: new[] { "account_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_account_ledger_entries_document_id",
                table: "account_ledger_entries",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_charges_expense_category_id",
                table: "bill_charges",
                column: "expense_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_charges_paid_from_account_id",
                table: "bill_charges",
                column: "paid_from_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_charges_purchase_bill_id",
                table: "bill_charges",
                column: "purchase_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_charges_sale_bill_id",
                table: "bill_charges",
                column: "sale_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id_name",
                table: "categories",
                columns: new[] { "parent_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_categories_name",
                table: "expense_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_accounts_name",
                table: "financial_accounts",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_expiry_date",
                table: "inventory_batches",
                column: "expiry_date");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_product_id_received_on_created_at",
                table: "inventory_batches",
                columns: new[] { "product_id", "received_on", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_batch_id",
                table: "inventory_movements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_document_id",
                table: "inventory_movements",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_product_id_date",
                table: "inventory_movements",
                columns: new[] { "product_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_balance",
                table: "parties",
                column: "balance");

            migrationBuilder.CreateIndex(
                name: "ix_parties_code",
                table: "parties",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parties_name",
                table: "parties",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_party_ledger_entries_document_id",
                table: "party_ledger_entries",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_party_ledger_entries_party_id_date",
                table: "party_ledger_entries",
                columns: new[] { "party_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_products_sku",
                table: "products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_unit_id",
                table: "products",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bill_lines_batch_id",
                table: "purchase_bill_lines",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bill_lines_product_id",
                table: "purchase_bill_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bill_lines_purchase_bill_id",
                table: "purchase_bill_lines",
                column: "purchase_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bills_date",
                table: "purchase_bills",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bills_number",
                table: "purchase_bills",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_bills_party_id",
                table: "purchase_bills",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_lines_batch_id",
                table: "purchase_return_lines",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_lines_product_id",
                table: "purchase_return_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_lines_purchase_bill_line_id",
                table: "purchase_return_lines",
                column: "purchase_bill_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_lines_purchase_return_id",
                table: "purchase_return_lines",
                column: "purchase_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_date",
                table: "purchase_returns",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_number",
                table: "purchase_returns",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_party_id",
                table: "purchase_returns",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_purchase_bill_id",
                table: "purchase_returns",
                column: "purchase_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_bill_lines_product_id",
                table: "sale_bill_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_bill_lines_sale_bill_id",
                table: "sale_bill_lines",
                column: "sale_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_bills_date",
                table: "sale_bills",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_sale_bills_number",
                table: "sale_bills",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_bills_party_id",
                table: "sale_bills",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_line_allocations_batch_id",
                table: "sale_line_allocations",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_line_allocations_sale_bill_line_id",
                table: "sale_line_allocations",
                column: "sale_bill_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_return_lines_product_id",
                table: "sale_return_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_return_lines_sale_bill_line_id",
                table: "sale_return_lines",
                column: "sale_bill_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_return_lines_sale_return_id",
                table: "sale_return_lines",
                column: "sale_return_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_returns_date",
                table: "sale_returns",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_sale_returns_number",
                table: "sale_returns",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_returns_party_id",
                table: "sale_returns",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_returns_sale_bill_id",
                table: "sale_returns",
                column: "sale_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_batch_id",
                table: "stock_adjustments",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_number",
                table: "stock_adjustments",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_product_id",
                table: "stock_adjustments",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_units_name",
                table: "units",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_account_id",
                table: "vouchers",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_date",
                table: "vouchers",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_expense_category_id",
                table: "vouchers",
                column: "expense_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_linked_document_id",
                table: "vouchers",
                column: "linked_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_number",
                table: "vouchers",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_party_id",
                table: "vouchers",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_to_account_id",
                table: "vouchers",
                column: "to_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_ledger_entries");

            migrationBuilder.DropTable(
                name: "bill_charges");

            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "party_ledger_entries");

            migrationBuilder.DropTable(
                name: "purchase_return_lines");

            migrationBuilder.DropTable(
                name: "sale_line_allocations");

            migrationBuilder.DropTable(
                name: "sale_return_lines");

            migrationBuilder.DropTable(
                name: "stock_adjustments");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vouchers");

            migrationBuilder.DropTable(
                name: "purchase_bill_lines");

            migrationBuilder.DropTable(
                name: "purchase_returns");

            migrationBuilder.DropTable(
                name: "sale_bill_lines");

            migrationBuilder.DropTable(
                name: "sale_returns");

            migrationBuilder.DropTable(
                name: "expense_categories");

            migrationBuilder.DropTable(
                name: "financial_accounts");

            migrationBuilder.DropTable(
                name: "inventory_batches");

            migrationBuilder.DropTable(
                name: "purchase_bills");

            migrationBuilder.DropTable(
                name: "sale_bills");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "parties");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "units");
        }
    }
}
