from http import HTTPStatus
from typing import Any

from django.core.files.uploadedfile import SimpleUploadedFile
from django.conf import settings

from base import TestViewSetBase
from test.fixtures.factories import UserFactory


class TestUserViewSet(TestViewSetBase):
    basename = "users"
    max_size: int
    over_sized_file: SimpleUploadedFile
    wrong_format_file: SimpleUploadedFile
    wrong_extension: str

    @classmethod
    def setUpTestData(cls) -> None:
        super().setUpTestData()
        cls.test_user_attributes = UserFactory.build()

        cls.max_size = settings.UPLOAD_MAX_SIZES["avatar_picture"]
        over_size = cls.max_size + 1
        correct_size = cls.max_size - 1
        cls.wrong_extension = "pdf"
        cls.over_sized_file = SimpleUploadedFile(
            "some_name.jpg", b"X" * over_size, content_type="image/jpg"
        )
        cls.wrong_format_file = SimpleUploadedFile(
            f"some_name.{cls.wrong_extension}",
            b"X" * correct_size,
            content_type="image/jpg",
        )

    @staticmethod
    def expected_details(entity: dict, attributes: dict[str, Any]) -> dict[str, Any]:
        return {
            **attributes,
            "id": entity["id"],
            "role": entity["role"],
            "avatar_picture": entity["avatar_picture"],
        }

    def test_create(self) -> dict[str, Any]:
        user = self.create(self.test_user_attributes)
        expected_response = self.expected_details(user, self.test_user_attributes)
        assert user == expected_response
        return user

    def test_retrieve(self) -> None:
        user = self.test_create()
        new_user_id = user["id"]
        response = self.retrieve(new_user_id)
        assert response == user

    def test_list(self) -> None:
        new_user = self.test_create()
        api_user = self.retrieve(self.user.id)
        response = self.list()
        assert len(response) == 2
        assert api_user in response
        assert new_user in response

    def test_update(self) -> None:
        user = self.test_create()
        new_user_id = user["id"]
        user_name = user["username"]
        updated_name = "Max"
        response = self.update(
            {"username": user_name, "first_name": updated_name}, new_user_id
        )
        assert response["first_name"] == updated_name

    def test_delete(self) -> None:
        new_user = self.test_create()
        users_list = self.list()
        assert new_user in users_list

        new_user_id = new_user["id"]
        response = self.delete(new_user_id)
        new_users_list = self.list()
        assert response.content == b""
        assert new_user not in new_users_list

    def test_oversize_avatar(self) -> None:
        oversize_avatar_user_data = {
            **self.test_user_attributes,
            "avatar_picture": self.over_sized_file,
        }
        response = self.request_create(
            data=oversize_avatar_user_data, format="multipart"
        )
        response_data = response.json()

        assert response.status_code == HTTPStatus.BAD_REQUEST
        assert response_data["avatar_picture"] == [
            f"Maximum size {self.max_size} exceeded."
        ]

    def test_wrong_file_format(self) -> None:
        oversize_avatar_user_data = {
            **self.test_user_attributes,
            "avatar_picture": self.wrong_format_file,
        }
        response = self.request_create(
            data=oversize_avatar_user_data, format="multipart"
        )
        response_data = response.json()

        assert response.status_code == HTTPStatus.BAD_REQUEST
        assert response_data["avatar_picture"] == [
            f"File extension “{self.wrong_extension}” is not allowed. Allowed extensions are: jpeg, jpg, png."
        ]
