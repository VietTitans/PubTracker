from sqlalchemy import Column, Integer, String, Boolean, DateTime
from sqlalchemy.orm import relationship
from .base import Base
from datetime import datetime

class Subscriber(Base):
    __tablename__ = "subscribers"

    id = Column(Integer, primary_key=True)
    email = Column(String(255), nullable=False, unique=True)
    is_active = Column(Boolean, nullable=False, default=True)
    created_at = Column(DateTime, nullable=False, default=datetime.utcnow)

    saved_searches = relationship("saved_searches", back_populates="subscriber")  # owned searches
    subscribed_searches = relationship(
        "saved_searches",
        secondary="subscriber_saved_search_subscriptions",
        back_populates="followers"
    )
    notifications = relationship("Notification", back_populates="subscriber", cascade="all, delete-orphan")
