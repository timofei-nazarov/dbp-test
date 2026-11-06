from typing import Any

from celery.result import AsyncResult
from django.conf import settings
from django.core.files.base import File
from django.core.validators import FileExtensionValidator
from rest_framework import serializers
from rest_framework.exceptions import ValidationError

from task_manager.tasks import countdown
from .models import User, Task, Tag


class FileMaxSizeValidator:
    def __init__(self, max_size: int) -> None:
        self.max_size = max_size

    def __call__(self, value: File) -> None:
        if value.size > self.max_size:
            raise ValidationError(f"Maximum size {self.max_size} exceeded.")


class UserSerializer(serializers.ModelSerializer):
    avatar_picture = serializers.FileField(
        required=False,
        validators=[
            FileMaxSizeValidator(settings.UPLOAD_MAX_SIZES["avatar_picture"]),
            FileExtensionValidator(["jpeg", "jpg", "png"]),
        ],
    )

    class Meta:
        model = User
        fields = (
            "id",
            "username",
            "first_name",
            "last_name",
            "email",
            "role",
            "avatar_picture",
        )


class TagSerializer(serializers.ModelSerializer):
    class Meta:
        model = Tag
        fields = ("id", "title", "unique_id")


class TaskSerializer(serializers.ModelSerializer):
    tag = TagSerializer(many=True, required=False)
    author = UserSerializer(required=False)
    executor = UserSerializer(required=False)

    class Meta:
        model = Task
        fields = (
            "id",
            "title",
            "description",
            "date_created",
            "date_updated",
            "date_to_finish",
            "condition",
            "priority",
            "author",
            "executor",
            "tag",
        )

    def create(self, data_to_create: dict) -> Task:
        for key in ["author", "executor"]:
            if key in data_to_create:
                user_data = data_to_create.get(key)
                user = User.objects.create(**user_data)
                data_to_create[key] = user
        if "tag" in data_to_create:
            tag_list = data_to_create.pop("tag")
            tag_instance_list = []
            for tag in tag_list:
                tag_instance = Tag.objects.create(**tag)
                tag_instance_list.append(tag_instance)
            task = Task.objects.create(**data_to_create)
            task.tag.set(tag_instance_list)
        else:
            task = Task.objects.create(**data_to_create)
        return task


class RepresentationSerializer(serializers.Serializer):
    def update(self, instance: Any, validated_data: dict) -> Any:
        pass

    def create(self, validated_data: dict) -> Any:
        pass


class CountdownJobSerializer(RepresentationSerializer):
    seconds = serializers.IntegerField(write_only=True)

    task_id = serializers.CharField(read_only=True)
    status = serializers.CharField(read_only=True)

    def create(self, validated_data: dict) -> AsyncResult:
        return countdown.delay(**validated_data)


class ErrorSerializer(RepresentationSerializer):
    non_field_errors: serializers.ListSerializer = serializers.ListSerializer(
        child=serializers.CharField()
    )


class JobSerializer(RepresentationSerializer):
    task_id = serializers.CharField(read_only=True)
    status = serializers.CharField(read_only=True)
    errors = ErrorSerializer(read_only=True, required=False)
    result = serializers.CharField(required=False)
