# DTO Implementation Guide

## What is a DTO?

A **DTO (Data Transfer Object)** is a simple object used to transfer data between your API and clients. It separates your internal domain model from your API contract, providing several benefits:

- **Security**: Hide sensitive fields from the API response
- **Flexibility**: API contracts remain stable even if internal models change
- **Consistency**: Control exactly what data is sent/received
- **Validation**: Can add validation rules specific to API requests

## Project Structure

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

## DTOs Created

### 1. **UserResponseDto**
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

**Usage:**
```csharp
var user = await _userService.GetUserByIdAsync(id);
return Ok(user.ToResponseDto()); // Converts User -> UserResponseDto
```

### 2. **CreateUserDto**
Used when clients POST a new user.

```csharp
public class CreateUserDto
{
	public string Name { get; set; }
	public string Username { get; set; }
	public string Email { get; set; }
}
```

**Usage (future endpoint):**
```csharp
[HttpPost]
public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
{
	var user = dto.ToUserModel(); // Converts CreateUserDto -> User
	await _userService.SaveUserAsync(user);
	return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user.ToResponseDto());
}
```

### 3. **UpdateUserDto**
Used when clients PUT/PATCH an existing user.

```csharp
public class UpdateUserDto
{
	public string Name { get; set; }
	public string Username { get; set; }
	public string Email { get; set; }
}
```

**Usage (future endpoint):**
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

## Mapping Methods

All mapping is handled by extension methods in `UserMappingExtensions.cs`:

| Method | Converts | Usage |
|--------|----------|-------|
| `ToResponseDto()` | User → UserResponseDto | Convert single user for response |
| `ToResponseDtoList()` | List<User> → List<UserResponseDto> | Convert list of users |
| `ToUserModel()` | CreateUserDto → User | Convert create request to model |
| `UpdateFromDto()` | UpdateUserDto → User | Update existing user from request |

**Examples:**

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

## Current API Endpoints (with DTOs)

All endpoints now return `UserResponseDto` instead of raw User models:

```http
GET /api/users/me
Headers: X-User-Id: 1
Response: UserResponseDto

GET /api/users/{id}
Headers: X-User-Id: 1
Response: UserResponseDto

GET /api/users/public/{id}
Response: UserResponseDto

GET /api/users
Headers: X-User-Id: 1, X-User-Role: Admin
Response: List<UserResponseDto>
```

## Adding More DTOs

To add DTOs for another entity (e.g., Venues):

1. **Create the DTOs:**
```csharp
// api/RecordService/Dtos/VenueResponseDto.cs
public class VenueResponseDto { ... }

// api/RecordService/Dtos/CreateVenueDto.cs
public class CreateVenueDto { ... }
```

2. **Create mapping extensions:**
```csharp
// Add to api/RecordService/Extensions/UserMappingExtensions.cs
public static VenueResponseDto ToResponseDto(this Venue venue) { ... }
public static Venue ToVenueModel(this CreateVenueDto dto) { ... }
```

3. **Use in controller:**
```csharp
var venueDto = venue.ToResponseDto();
return Ok(venueDto);
```

## Validation with DTOs

You can add validation attributes to DTOs for automatic server-side validation:

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

ASP.NET Core automatically validates on POST/PUT requests!

## Manual Mapping vs AutoMapper

**Current Implementation: Manual Mapping**
- ✅ No external dependencies
- ✅ Simple and easy to understand
- ✅ Full control over mapping logic
- ❌ More code for complex models

**Alternative: AutoMapper** (for future use)
If you have many DTOs or complex mappings, consider using AutoMapper NuGet package.

## Best Practices

1. **Always return DTOs** from API endpoints, never domain models
2. **Create specific DTOs** for different use cases:
   - `ResponseDto` - What the API returns
   - `CreateDto` - What POST requests send
   - `UpdateDto` - What PUT/PATCH requests send
3. **Hide sensitive data** - Don't include passwords, hashes, etc. in DTOs
4. **Use mapping extensions** - Keeps code DRY and centralized
5. **Validate in DTOs** - Use data annotations for validation rules
6. **Document your DTOs** - Add XML comments for API documentation

## Future: Adding Create/Update Endpoints

When ready to add POST/PUT endpoints, the DTOs are already configured:

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
