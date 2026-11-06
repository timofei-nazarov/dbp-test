from django.test import TestCase
from rest_framework import status
from rest_framework.test import APIClient

from main.models import Task, User, Tag


class PermissionTestCase(TestCase):
    def setUp(self) -> None:
        self.client = APIClient()

        self.regular_user = User.objects.create_user(
            username="user1", password="password1", is_staff=False
        )
        self.stuff_user = User.objects.create_user(
            username="user2", password="password2", is_staff=True
        )

        self.task1 = Task.objects.create(
            title="Task 1", description="Description 1", author=self.regular_user
        )
        self.task2 = Task.objects.create(
            title="Task 2", description="Description 2", author=self.stuff_user
        )
        self.test_tag = Tag.objects.create(title="test_tag", unique_id="333")

    def test_user_can_read(self) -> None:
        self.client.force_authenticate(self.regular_user)
        response = self.client.get("/api/tasks/")
        self.assertEqual(response.status_code, status.HTTP_200_OK)

    def test_admin_can_read(self) -> None:
        self.client.force_authenticate(self.stuff_user)
        response = self.client.get("/api/tags/")
        self.assertEqual(response.status_code, status.HTTP_200_OK)

    def test_user_cannot_delete_task(self) -> None:
        self.client.force_authenticate(self.regular_user)
        response = self.client.delete(f"/api/tasks/{self.task2.id}/")
        self.assertEqual(response.status_code, status.HTTP_403_FORBIDDEN)

    def test_admin_can_delete_task(self) -> None:
        self.client.force_authenticate(self.stuff_user)
        response = self.client.delete(f"/api/tasks/{self.task1.id}/")
        self.assertEqual(response.status_code, status.HTTP_204_NO_CONTENT)

    def test_admin_can_delete_tag(self) -> None:
        self.client.force_authenticate(self.stuff_user)
        response = self.client.delete(f"/api/tags/{self.test_tag.id}/")
        self.assertEqual(response.status_code, status.HTTP_204_NO_CONTENT)
