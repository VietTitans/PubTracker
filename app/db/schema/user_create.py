from pydantic import BaseModel, EmailStr, Field

class UserCreate(BaseModel):
    username: str = Field(..., description="Username for the user")
    email: EmailStr = Field(..., description="Email address")