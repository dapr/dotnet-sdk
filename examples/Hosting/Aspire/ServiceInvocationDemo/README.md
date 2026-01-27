# Aspire with Dapr Service Invocation Demo

This directory contains demonstration projects showcasing how to use Aspire with Dapr to perform service invocation between frontend and backend applications using different approaches.

## Overview

The ServiceInvocationDemo demonstrates various patterns for service-to-service communication in a distributed application architecture using:

- **Aspire** - For application orchestration, service discovery, and observability
- **Dapr** - For distributed application runtime capabilities including service invocation
- **ASP.NET Core** - For both frontend and backend services

## Project Structure

The demo includes:
- **Frontend Application** - Web application that invokes backend services
- **Backend Application** - API services that handle business logic
- **AppHost** - Aspire orchestration project that coordinates all services
- Multiple service invocation approaches and patterns

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Docker Desktop (for Dapr components)
- Dapr CLI installed and initialized

### Running the Demo

1. Navigate to the ServiceInvocationDemo.AppHost project directory:
   ```bash
   cd examples/Hosting/Aspire/ServiceInvocationDemo/ServiceInvocationDemo.AppHost
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. The console will display the Aspire dashboard URL. Open the provided link in your browser.

4. In the Aspire dashboard, click on the "FrontendApp.csproj" link `http://localhost:5054/` to access the demo application.

5. The main page will present various service invocation demonstration scenarios that you can explore.

## Key Features Demonstrated

- Integration between Aspire orchestration and Dapr runtime
- Service-to-service communication patterns
- Development-time productivity with Aspire dashboard
- Production-ready distributed application patterns

## No Additional Setup Required

The demo is designed to work out-of-the-box with minimal setup. Simply run `dotnet run` from the AppHost project, and all necessary services will be orchestrated automatically through Aspire's application model.

## Architecture Benefits

This demonstration highlights the benefits of combining Aspire and Dapr:

- **Simplified Local Development** - Aspire handles service orchestration and provides rich debugging experience
- **Production Readiness** - Dapr provides rich distributed application patterns
- **Flexibility** - Multiple approaches to solve service invocation challenges

Explore the different demo scenarios to understand how these technologies work together to build resilient and maintainable distributed applications.

## SDK docs

For more information on the .NET Dapr client visit the [SDK docs](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-client/).