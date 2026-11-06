from test.base import TestViewSetBase


class TestUserViewSet(TestViewSetBase):
    basename = "current_user"
    test_host: str

    @classmethod
    def setUpTestData(cls) -> None:
        super().setUpTestData()
        cls.test_host = "http://testserver"
        cls.expected_user_response = {
            "id": cls.user.id,
            "email": cls.user.email,
            "first_name": cls.user.first_name,
            "last_name": cls.user.last_name,
            "role": cls.user.role,
            "username": cls.user.username,
            "avatar_picture": f"{cls.test_host}{cls.user.avatar_picture.url}",
        }

    def test_retrieve(self):
        user = self.single_resource()
        assert user == self.expected_user_response

    def test_patch(self):
        self.patch_single_resource({"first_name": "TestName"})
        user = self.single_resource()

        assert user["first_name"] == "TestName"
