StudyHub - Engineering & Coding Standards
1. Architectural Principles (Clean Architecture)
Dependency Rule: Source code dependencies must strictly point inwards, toward the Domain layer.

Layer Isolation: The Domain layer must have zero dependencies on any external frameworks, including Entity Framework Core or ASP.NET Core.

Persistence Ignorance: Database concerns (e.g., table names, column types) must not leak into domain entities. All EF Core configurations must be handled via the Fluent API in the Infrastructure layer, completely avoiding Data Annotations in the domain.

2. C# & .NET 10 Development Standards
Encapsulation: Domain entities must protect their state. Use private set or init for properties. Parameterless constructors required by EF Core must be marked as protected or private.

Nullability: Nullable reference types are enabled globally. Explicitly mark nullable properties with ?.

Asynchronous Programming: All I/O bound operations (Database queries, external API calls) must be fully asynchronous. Suffix methods with Async and always pass CancellationToken.

3. Naming Conventions
Classes, Interfaces, and Records: Use PascalCase. Interfaces must always be prefixed with I (e.g., IUserRepository).

Properties and Methods: Use PascalCase.

Local Variables and Parameters: Use camelCase.

Private Fields: Use camelCase prefixed with an underscore (e.g., _dbContext).

4. API & Error Handling Design
RESTful Compliance: Controllers must adhere to standard HTTP verbs (GET, POST, PUT, DELETE) and return appropriate status codes (e.g., 201 Created, 404 Not Found, 204 No Content).

Global Exception Handling: Do not use try-catch blocks in controllers for standard domain exceptions. Implement a centralized Global Exception Handling Middleware to intercept errors and return a standardized JSON response.

Data Transfer Objects (DTOs): Never expose domain entities directly through the API. Always map entities to DTOs before returning them to the client.

5. Version Control Workflow
Branching Strategy: Direct commits to the main branch are prohibited. Create branches using the format: feature/[ticket-name], bugfix/[ticket-name], or chore/[ticket-name].

Commit Messages: Follow Conventional Commits formatting (e.g., feat(domain): add task entity, fix(api): resolve token validation error).
