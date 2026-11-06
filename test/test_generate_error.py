from django.urls import reverse
from rest_framework.test import APIClient, APITestCase


class TestGenerateError(APITestCase):
    client: APIClient
    error_url: str = reverse("rollbar_error")

    @classmethod
    def setUpTestData(cls) -> None:
        super().setUpTestData()
        cls.client = APIClient()

    def test_generate_error(self) -> None:
        with self.assertRaises(AttributeError):
            self.client.get(self.error_url)
