# Tuition Centre Management System

A web-based tuition centre management application developed as an academic group project using C#, ASP.NET Core MVC, and SQL Server.

## Overview

The Tuition Centre Management System brings together common tuition-centre workflows in one web application. The source code is organized around ASP.NET Core MVC controllers, models, Razor views, and a SQL Server database accessed through Entity Framework Core.

## Technology Stack

- **Language:** C#
- **Web framework:** ASP.NET Core MVC
- **Database:** Microsoft SQL Server
- **ORM and migrations:** Entity Framework Core
- **Authentication:** Cookie authentication with role-based authorization
- **Email:** MailKit and MimeKit
- **PDF generation:** QuestPDF
- **Frontend:** Razor Views, HTML, CSS, and JavaScript where used in the project

## Features

The repository contains code for the following areas. The final public README should describe only features that have been tested and are working in the version being shared.

### User accounts and profiles
- User registration and login
- Email verification flow
- Forgot-password and reset-password flows
- Change-password and profile management
- Role-based access for Admin, Teacher, and Student areas

### Student and academic management
- Student records management
- Teacher records management
- Subject management
- Tuition class management
- Student enrolment and class timetable views
- Teacher-to-class assignments

### Attendance
- Attendance record management
- Attendance sessions with time-limited numeric check-in codes
- Attendance views and filtering
- Monthly attendance PDF reports
- Teacher-facing student and attendance reports

### Billing and payments
- Student bills and payment records
- Student billing and checkout pages
- Payment status updates and revenue reporting
- Downloadable PDF payment receipts

**Payment note:** The current checkout code records a simulated payment as completed. Do not describe this as a live payment-gateway integration unless a real provider has been integrated and verified.

## Architecture

The application follows the ASP.NET Core MVC pattern:

- **Controllers/** — handles requests and coordinates application workflows.
- **Models/** — defines domain entities and view models.
- **Views/** — contains Razor pages for the user interface.
- **Data/** — contains the Entity Framework Core database context.
- **Migrations/** — records database schema changes.
- **Services/** — contains supporting services, including email and PDF generation.
- **wwwroot/** — contains static assets such as stylesheets, scripts, and images.

The `ApplicationDbContext` defines entities for users, subjects, tuition classes, enrolments, attendance, attendance sessions, payments, and bills.

## Running the Project

### Prerequisites

- A compatible .NET SDK for the target framework in the project file (`net10.0`)
- Microsoft SQL Server or a compatible local SQL Server instance
- Visual Studio or another .NET development environment

### Setup

1. Clone this repository.
2. Open the solution file in the repository.
3. Configure a local SQL Server connection string using user secrets, environment variables, or another local configuration method.
4. Configure email settings locally if you want to test email-dependent flows.
5. Restore NuGet packages.
6. Apply the required Entity Framework Core migrations or follow the database setup instructions supplied with the project.
7. Run the ASP.NET Core application and open the local URL shown by the development server.

Exact commands and database setup steps should be added after confirming the solution file, SDK, database configuration, and migration workflow on a clean machine.

This project provided experience with building a multi-module web application using C# and ASP.NET Core MVC, structuring an application with controllers, models, and views, integrating SQL Server through Entity Framework Core, and collaborating on a group software project.

## Project Information

- **Type:** Academic group project
- **Status:** Completed
- **Primary technologies:** C#, ASP.NET Core MVC, SQL Server
