from rest_framework.utils.serializer_helpers import ReturnDict

from base import TestViewSetBase


class TestTaskViewSet(TestViewSetBase):
    basename = "tasks"
    user_attributes = {
        "username": "johnsmith",
        "first_name": "John",
        "last_name": "Smith",
        "email": "john@test.com",
        "is_staff": True,
    }

    task_data = {
        "title": "task_to_test",
        "description": "test_description",
        "date_to_finish": "2023-05-30",
        "priority": 1,
        "author": {
            "username": "author",
            "first_name": "John",
            "last_name": "Smith",
            "email": "john@test.com",
        },
        "executor": {
            "username": "executor",
            "first_name": "John",
            "last_name": "Smith",
            "email": "john@test.com",
        },
        "tag": [
            {"title": "nested_tag"},
        ],
    }

    @staticmethod
    def expected_details(entity: dict, attributes: dict) -> dict:
        expected_dict = {
            **attributes,
            "id": entity["id"],
            "date_created": entity["date_created"],
            "date_updated": entity["date_updated"],
            "condition": entity["condition"],
            "executor": entity["executor"],
            "author": entity["author"],
            "tag": entity["tag"],
        }
        return expected_dict

    def test_create(self) -> ReturnDict:
        task = self.create(data=self.task_data, format="json")
        expected_response = self.expected_details(task, self.task_data)
        assert task == expected_response
        return task

    def test_retrieve(self) -> None:
        task = self.create(data=self.task_data, format="json")
        task_id = task["id"]
        response = self.retrieve(task_id)
        assert response == task

    def test_list(self) -> None:
        new_task = self.test_create()
        response = self.list()
        assert len(response) == 1
        assert new_task in response

    def test_update(self) -> None:
        task = self.test_create()
        new_title = "title updated"
        response = self.update({"title": new_title}, task["id"])
        assert response["title"] == new_title

    def test_delete(self) -> None:
        task = self.test_create()
        task_id = task["id"]
        assert task in self.list()

        response = self.delete(task_id)
        assert response.content == b""
        assert task not in self.list()
