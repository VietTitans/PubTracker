from pydantic import BaseModel, EmailStr

class SubscriberCreate(BaseModel):
    email: EmailStr
    is_active: bool = True