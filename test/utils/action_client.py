from http import HTTPStatus
from typing import Optional, Any

from rest_framework.test import APIClient

from main.models import User
from test.utils.model_resource import UserResource, TaskResource, TagResource


class ActionClient:
    def __init__(self, api_client: APIClient) -> None:
        self.api_client = api_client
        self.user: Optional[User] = None
        self.users = UserResource()
        self.tasks = TaskResource()
        self.tags = TagResource()

    def init_user(self) -> None:
        self.user = self.users.create(is_staff=True)
        self.api_client.force_authenticate(user=self.user)
