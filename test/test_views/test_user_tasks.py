from http import HTTPStatus

from test.base import TestViewSetBase


class TestUserTasksViewSet(TestViewSetBase):
    basename = "user_tasks"

    def test_list(self) -> None:
        user = self.action_client.users.create()
        task = self.action_client.tasks.create(executor_id=user.id)

        tasks = self.list(args=[user.id])

        assert len(tasks) == 1
        assert tasks[0]["id"] == task.id
        assert tasks[0]["executor"]["id"] == task.executor_id

    def test_retrieve_user_task(self) -> None:
        user = self.action_client.users.create()
        task = self.action_client.tasks.create(executor_id=user.id)

        response = self.request_retrieve([user.id, task.id])
        response_data = response.data

        assert response.status_code == HTTPStatus.OK
        assert response_data["id"] == task.id
        assert response_data["executor"]["id"] == user.id

    def test_retrieve_foreign_task(self) -> None:
        user = self.action_client.users.create()
        task = self.action_client.tasks.create()

        response = self.request_retrieve([user.id, task.id])

        assert response.status_code == HTTPStatus.NOT_FOUND
        assert response.json() == {"detail": "Not found."}
