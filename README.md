# Lake Country Spanish

A complete web application for managing an online Spanish teaching business. Built with ASP.NET Core 8 MVC, this application provides student scheduling, payment processing via Stripe, document management, and a modern responsive interface.

## Features

### For Students
- **Dashboard** - View upcoming classes, account balance, and learning documents
- **Class Booking** - Browse available time slots and book lessons
- **Package Credits** - Purchase class packages for discounted rates
- **Payment Processing** - Secure payments via Stripe with automatic verification
- **Document Access** - Download learning materials shared by the teacher

### For Administrators
- **Dashboard** - Overview of today's schedule, revenue, and new inquiries
- **Student Management** - Create and manage student accounts with custom pricing
- **Schedule Management** - View all classes, mark complete, or cancel
- **Time Slot Configuration** - Set recurring weekly availability or specific dates
- **Payment Tracking** - Monitor all transactions with filtering options
- **Document Management** - Upload and assign learning materials to students
- **Contact Inquiries** - Review and respond to potential student messages

### Public Features
- **Home Page** - Attractive landing page with pricing and information
- **About Page** - Teacher introduction and teaching philosophy
- **Contact Form** - reCAPTCHA-protected inquiry form for prospective students

## Technology Stack

- **Framework**: ASP.NET Core 8.0 MVC with Razor Views
- **Database**: SQL Server LocalDB with Entity Framework Core
- **Authentication**: ASP.NET Core Identity (Admin/Student roles)
- **Payments**: Stripe integration with webhook support
- **Styling**: Tailwind CSS (via CDN)
- **Security**: Google reCAPTCHA v3 for contact form protection

## Project Structure

```
SpanishScheduler/
├── README.md
├── docs/                          # Documentation
├── src/
│   ├── SpanishScheduler.sln       # Solution file
│   └── SpanishScheduler.Web/      # Main web application
│       ├── Controllers/           # MVC Controllers
│       ├── Data/                  # DbContext and seed data
│       ├── Migrations/            # EF Core migrations
│       ├── Models/
│       │   ├── Entities/          # Database entities
│       │   └── ViewModels/        # View models
│       ├── Services/              # Business logic services
│       ├── Views/                 # Razor views
│       └── wwwroot/               # Static files
└── .gitignore
```

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server LocalDB](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio)
- [Stripe Account](https://stripe.com) (free to create)
- [Google reCAPTCHA](https://www.google.com/recaptcha) (optional, for contact form)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/YOUR_USERNAME/SpanishScheduler.git
   cd SpanishScheduler
   ```

2. **Configure application settings**

   Update `src/SpanishScheduler.Web/appsettings.json` with your keys:
   ```json
   {
     "Stripe": {
       "PublishableKey": "pk_test_your_key_here",
       "SecretKey": "sk_test_your_key_here",
       "WebhookSecret": "whsec_your_webhook_secret_here"
     },
     "ReCaptcha": {
       "SiteKey": "your_site_key_here",
       "SecretKey": "your_secret_key_here"
     }
   }
   ```

3. **Run the application**
   ```bash
   cd src/SpanishScheduler.Web
   dotnet run
   ```

4. **Access the application**

   Open your browser to `https://localhost:5001` or `http://localhost:5000`

### Default Admin Account

On first run, the application seeds a default admin account:

- **Email**: `admin@lakecountryspanish.com`
- **Password**: `Admin123!`

> **Important**: Change this password immediately after first login in a production environment.

## Configuration

### Stripe Setup

1. Create a [Stripe account](https://dashboard.stripe.com/register)
2. Get your API keys from the [Stripe Dashboard](https://dashboard.stripe.com/apikeys)
3. For webhooks (production), configure an endpoint at `/api/payment/webhook`
4. Add the webhook signing secret to your configuration

### reCAPTCHA Setup (Optional)

1. Register your site at [Google reCAPTCHA](https://www.google.com/recaptcha/admin)
2. Choose reCAPTCHA v3
3. Add the site key and secret key to your configuration

### Database

The application uses SQL Server LocalDB by default. The database is created automatically on first run.

To use a different SQL Server instance, update the connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=SpanishScheduler;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## Default Pricing Packages

The application seeds four default class packages:

| Package | Classes | Price | Per Class |
|---------|---------|-------|-----------|
| Single Class | 1 | $25 | $25.00 |
| 5 Class Package | 5 | $115 | $23.00 |
| 10 Class Package | 10 | $220 | $22.00 |
| 20 Class Package | 20 | $420 | $21.00 |

These can be modified through the Admin dashboard.

## Custom Student Pricing

Administrators can set custom hourly rates per student for special pricing agreements. When a custom rate is set, it overrides the default class price for that student.

## Development

### Running Migrations

```bash
cd src/SpanishScheduler.Web
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Building

```bash
cd src
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Deployment

### Environment Variables

For production, use environment variables instead of `appsettings.json` for sensitive data:

- `ConnectionStrings__DefaultConnection`
- `Stripe__PublishableKey`
- `Stripe__SecretKey`
- `Stripe__WebhookSecret`
- `ReCaptcha__SiteKey`
- `ReCaptcha__SecretKey`

### Stripe Webhooks

In production, configure Stripe webhooks to notify your application of payment events:

1. Go to Stripe Dashboard > Developers > Webhooks
2. Add endpoint: `https://yourdomain.com/api/payment/webhook`
3. Select events: `checkout.session.completed`
4. Copy the signing secret to your configuration

## License

This project is private and proprietary.

## Support

For issues or questions, please open an issue in this repository.
