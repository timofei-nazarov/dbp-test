from typing import Any

from django.db.models import Model
from factory import Factory

from main.models import User, Task, Tag
from test.fixtures.factories import UserFactory, TaskFactory, TagFactory


class ModelResourceBase:
    factory: Factory
    model: Model

    def create(self, **kwargs: Any) -> Model:
        attributes = self.factory.build(**kwargs)
        return self.model.objects.create(**attributes)


class UserResource:
    factory = UserFactory

    def create(self, **kwargs: Any) -> User:
        attributes = self.factory.build(**kwargs)
        return User.objects.create_user(**attributes)


class TaskResource(ModelResourceBase):
    factory = TaskFactory
    model = Task


class TagResource(ModelResourceBase):
    factory = TagFactory
    model = Tag
