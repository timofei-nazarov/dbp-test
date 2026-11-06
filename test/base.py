from typing import Union, List
from http import HTTPStatus

from django.urls import reverse
from rest_framework.response import Response
from rest_framework.test import APIClient, APITestCase

from main.models import User
from test.utils.action_client import ActionClient


class TestViewSetBase(APITestCase):
    user: User = None
    api_client: APIClient = None
    action_client: ActionClient
    basename: str

    @classmethod
    def setUpTestData(cls) -> None:
        super().setUpTestData()
        cls.api_client = APIClient()
        cls.action_client = ActionClient(cls.api_client)
        cls.action_client.init_user()
        cls.user = cls.action_client.user

    def setUp(self) -> None:
        self.api_client.force_authenticate(self.user)

    @classmethod
    def detail_url(cls, key: Union[int, str, list[Union[int, str]]] = None) -> str:
        args = key if isinstance(key, list) else [key]
        return reverse(f"{cls.basename}-detail", args=args)

    @classmethod
    def list_url(cls, args: List[Union[str, int]] = None) -> str:
        return reverse(f"{cls.basename}-list", args=args)

    def request_create(
        self, data: dict, args: List[Union[str, int]] = None, format: str = None
    ) -> Response:
        response = self.api_client.post(self.list_url(args), data=data, format=format)
        return response

    def create(
        self, data: dict, args: List[Union[str, int]] = None, format: str = None
    ) -> dict:
        response = self.request_create(args=args, data=data, format=format)
        assert response.status_code == HTTPStatus.CREATED
        return response.data

    def request_retrieve(
        self, args: Union[int, str, List[Union[str, int]]] = None
    ) -> Response:
        url = self.detail_url(args)
        response = self.api_client.get(url)
        return response

    def retrieve(self, args: Union[str, int, list[Union[str, int]]] = None) -> dict:
        response = self.request_retrieve(args)
        assert response.status_code == HTTPStatus.OK
        return response.data

    def delete(
        self, args: Union[str, int, list[Union[str, int]]] = None
    ) -> Response:
        response = self.api_client.delete(self.detail_url(args))
        assert response.status_code == HTTPStatus.NO_CONTENT
        return response

    def list(self, args: List[Union[str, int]] = None) -> dict:
        response = self.api_client.get(self.list_url(args))
        assert response.status_code == HTTPStatus.OK
        return response.data

    def update(self, data: dict, args: Union[str, int] = None) -> dict:
        response = self.api_client.put(self.detail_url(args), data=data)
        assert response.status_code == HTTPStatus.OK
        return response.data

    def request_partial_update(
        self, attributes: dict, args: Union[str, int, List[Union[str, int]]] = None
    ) -> Response:
        url = self.detail_url(args)
        return self.api_client.patch(url, data=attributes)

    def partial_update(
        self, attributes: dict, args: Union[str, int, List[Union[str, int]]] = None
    ) -> dict:
        response = self.request_partial_update(attributes, args=args)
        assert response.status_code == HTTPStatus.OK
        return response.data

    def request_single_resource(self, data: dict = None) -> Response:
        return self.api_client.get(self.list_url(), data=data)

    def single_resource(self, data: dict = None) -> dict:
        response = self.request_single_resource(data)
        assert response.status_code == HTTPStatus.OK
        return response.data

    def request_patch_single_resource(self, attributes: dict) -> Response:
        url = self.list_url()
        return self.api_client.patch(url, data=attributes)

    def patch_single_resource(self, attributes: dict) -> dict:
        response = self.request_patch_single_resource(attributes)
        assert response.status_code == HTTPStatus.OK, response.content
        return response.data
