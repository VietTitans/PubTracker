# DTO Implementation Guide

## What is a DTO?

A DTO (Data Transfer Object) carries data between the API and its clients and keeps the internal domain model out of the API contract. That hides sensitive fields from responses, keeps the contract stable when internal models change, controls exactly what is sent and received, and allows validation rules specific to API requests.

## Project structure

```
api/RecordService/
├── Dtos/
│   ├── UserResponseDto.cs      (API response)
│   ├── CreateUserDto.cs         (POST request)
│   └── UpdateUserDto.cs         (PUT/PATCH request)
├── Extensions/
│   └── UserMappingExtensions.cs (Mapping logic)
└── Controllers/
	└── UsersController.cs       (Uses DTOs)
```

## DTOs

### UserResponseDto
Used when returning user data in API responses. Excludes sensitive fields.

```csharp
public class UserResponseDto
{
	public int Id { get; set; }
	public string Name { get; set; }
	public string Username { get; set; }
	public string Email { get; set; }
}
```

Usage:
```csharp
var user = await _userService.GetUserByIdAsync(id);
return Ok(user.ToResponseDto()); // Converts User -> UserResponseDto
```

### CreateUserDto
Used when clients POST a new user.

```csharp
public class CreateUserDto
{
	public string Name { get; set; }
	public string Username { get; set; }
	public string Email { get; set; }
}
```

Usage (future endpoint):
```csharp
[HttpPost]
public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
{
	var user = dto.ToUserModel(); // Converts CreateUserDto -> User
	await _userService.SaveUserAsync(user);
	return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user.ToResponseDto());
}
```

### UpdateUserDto
Used when clients PUT/PATCH an existing user.

```csharp
public class UpdateUserDto
{
	public string Name { get; set; }
	public string Username { get; set; }
	public string Email { get; set; }
}
```

Usage (future endpoint):
```csharp
[HttpPut("{id}")]
public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
{
	var user = await _userService.GetUserByIdAsync(id);
	user.UpdateFromDto(dto); // Updates User from UpdateUserDto
	await _userService.SaveUserAsync(user);
	return Ok(user.ToResponseDto());
}
```

## Mapping methods

Extension methods in `UserMappingExtensions.cs` handle all mapping:

| Method | Converts | Usage |
|--------|----------|-------|
| `ToResponseDto()` | User → UserResponseDto | Convert single user for response |
| `ToResponseDtoList()` | List<User> → List<UserResponseDto> | Convert list of users |
| `ToUserModel()` | CreateUserDto → User | Convert create request to model |
| `UpdateFromDto()` | UpdateUserDto → User | Update existing user from request |

Examples:

```csharp
// Single user
var responseDto = user.ToResponseDto();

// List of users
var responseDtos = users.ToResponseDtoList();

// Create new user from DTO
var userModel = createDto.ToUserModel();

// Update existing user from DTO
existingUser.UpdateFromDto(updateDto);
```

## Current API endpoints

All endpoints return `UserResponseDto` instead of raw User models:

```http
GET /api/users/me
Headers: X-User-Id: 1
Response: UserResponseDto

GET /api/users/{id}
Headers: X-User-Id: 1
Response: UserResponseDto

GET /api/users
Headers: X-User-Id: 1, X-User-Role: Admin
Response: List<UserResponseDto>
```

## Adding more DTOs

To add DTOs for another entity (e.g., Venues):

1. Create the DTOs:
```csharp
// api/RecordService/Dtos/VenueResponseDto.cs
public class VenueResponseDto { ... }

// api/RecordService/Dtos/CreateVenueDto.cs
public class CreateVenueDto { ... }
```

2. Create mapping extensions:
```csharp
// Add to api/RecordService/Extensions/UserMappingExtensions.cs
public static VenueResponseDto ToResponseDto(this Venue venue) { ... }
public static Venue ToVenueModel(this CreateVenueDto dto) { ... }
```

3. Use in the controller:
```csharp
var venueDto = venue.ToResponseDto();
return Ok(venueDto);
```

## Validation with DTOs

Validation attributes on DTOs give automatic server-side validation:

```csharp
using System.ComponentModel.DataAnnotations;

public class CreateUserDto
{
	[Required(ErrorMessage = "Name is required")]
	[StringLength(100, MinimumLength = 2)]
	public string Name { get; set; }

	[Required]
	[StringLength(50)]
	public string Username { get; set; }

	[Required]
	[EmailAddress]
	public string Email { get; set; }
}
```

ASP.NET Core validates POST and PUT requests automatically.

## Manual mapping vs AutoMapper

The project maps manually. That needs no external dependencies, is simple to read, and gives full control over mapping logic. The cost is more code for complex models.

With many DTOs or complex mappings, the AutoMapper NuGet package is an alternative.

## Best practices

1. Always return DTOs from API endpoints, never domain models
2. Create a specific DTO for each use case:
   - `ResponseDto`: what the API returns
   - `CreateDto`: what POST requests send
   - `UpdateDto`: what PUT/PATCH requests send
3. Hide sensitive data: keep passwords, hashes, etc. out of DTOs
4. Use mapping extensions to keep mapping code in one place
5. Validate in DTOs with data annotations
6. Document DTOs with XML comments for API documentation

## Adding create and update endpoints

The DTOs for POST/PUT endpoints already exist:

```csharp
[HttpPost]
public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
{
	if (!ModelState.IsValid)
		return BadRequest(ModelState);

	var user = dto.ToUserModel();
	await _userService.SaveUserAsync(user);
	return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user.ToResponseDto());
}

[HttpPut("{id}")]
[Authorize(Policy = "UserOrAdmin")]
public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
{
	if (!ModelState.IsValid)
		return BadRequest(ModelState);

	var user = await _userService.GetUserByIdAsync(id);
	if (user == null)
		return NotFound();

	user.UpdateFromDto(dto);
	await _userService.SaveUserAsync(user);
	return Ok(user.ToResponseDto());
}
```
