from http import HTTPStatus

from main.models import Task, Tag
from test.base import TestViewSetBase


class TestTaskTagsViewSet(TestViewSetBase):
    basename = "task_tags"

    @staticmethod
    def add_tags(task: Task, tags: list[Tag]) -> None:
        tag_ids = [obj.id for obj in tags]
        task.tag.add(*tag_ids)
        task.save()

    @classmethod
    def create_task_with_tag(cls) -> tuple[Task, Tag]:
        task = cls.action_client.tasks.create()
        tag = cls.action_client.tags.create()
        cls.add_tags(task, [tag])
        return task, tag

    def test_list(self) -> None:
        task, tag1 = self.create_task_with_tag()
        tag2 = self.action_client.tags.create()
        self.add_tags(task, [tag2])

        tags = self.list(args=[task.id])

        assert len(tags) == 2
        assert tags[0]["id"] == tag1.id
        assert tags[1]["id"] == tag2.id

    def test_retrieve(self) -> None:
        task, tag = self.create_task_with_tag()

        response = self.retrieve([task.id, tag.id])

        assert response["id"] == tag.id
        assert response["title"] == tag.title

    def test_retrieve_foreign_tag(self) -> None:
        task = self.action_client.tasks.create()
        tag = self.action_client.tags.create()

        response = self.request_retrieve([task.id, tag.id])

        assert response.status_code == HTTPStatus.NOT_FOUND
        assert response.json() == {"detail": "Not found."}

    def test_create(self) -> None:
        task = self.action_client.tasks.create()

        task_tags = self.list([task.id])
        assert task_tags == []

        response = self.create(data={"title": "tag_title"}, args=[task.id])
        assert response["title"] == "tag_title"

        same_task_tag = self.list([task.id])
        assert len(same_task_tag) == 1
        assert same_task_tag[0]["title"] == "tag_title"

    def test_create_bad_name(self) -> None:
        task = self.action_client.tasks.create()
        response = self.request_create(data={"title": ""}, args=[task.id])
        assert response.json() == {"title": ["This field may not be blank."]}

    def test_partial_update(self) -> None:
        task, tag = self.create_task_with_tag()

        response = self.partial_update({"title": "new_title"}, args=[task.id, tag.id])

        assert response["id"] == tag.id
        assert response["title"] == "new_title"

    def test_delete(self) -> None:
        task, tag = self.create_task_with_tag()

        task_tags = self.list([task.id])
        assert len(task_tags) == 1
        assert task_tags[0]["id"] == tag.id

        response = self.delete([task.id, tag.id])
        assert response.data is None

        same_task_tags = self.list([task.id])
        assert same_task_tags == []
