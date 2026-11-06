import django_filters

from typing import cast, Any

from django.db.models import QuerySet
from django.http import HttpRequest, HttpResponse, Http404
from django.urls import reverse
from rest_framework import viewsets, mixins, status
from rest_framework.permissions import BasePermission, SAFE_METHODS, IsAuthenticated
from rest_framework.request import Request
from rest_framework.response import Response
from rest_framework.serializers import BaseSerializer
from rest_framework_extensions.mixins import NestedViewSetMixin

from .models import User, Task, Tag
from .serializers import (
    UserSerializer,
    TaskSerializer,
    TagSerializer,
    CountdownJobSerializer,
    JobSerializer,
)
from .services.async_celery import JobStatus, AsyncJob
from .services.single_resource import SingleResourceMixin, SingleResourceUpdateMixin


class AdminOrReadonly(BasePermission):
    def has_permission(self, request: HttpRequest, view: viewsets.ModelViewSet) -> bool:
        if request.user.is_staff:
            return True
        else:
            return request.method in SAFE_METHODS


class UserFilter(django_filters.FilterSet):
    name = django_filters.CharFilter(field_name="first_name", lookup_expr="icontains")

    class Meta:
        model = User
        fields = ("name",)


class TaskFilter(django_filters.FilterSet):
    condition = django_filters.ChoiceFilter(
        field_name="condition", choices=Task.Conditions.choices
    )
    tag = django_filters.ModelMultipleChoiceFilter(
        field_name="tag__title",
        to_field_name="title",
        conjoined=True,
        queryset=Tag.objects.all(),
    )
    executor = django_filters.ModelChoiceFilter(
        field_name="executor", queryset=User.objects.all()
    )
    author = django_filters.ModelChoiceFilter(
        field_name="author", queryset=User.objects.all()
    )

    class Meta:
        model = Task
        fields = ["condition", "tag", "executor", "author"]


class UserViewSet(viewsets.ModelViewSet):
    queryset = User.objects.order_by("id")
    serializer_class = UserSerializer
    filterset_class = UserFilter
    permission_classes = (IsAuthenticated, AdminOrReadonly)


class TagViewSet(viewsets.ModelViewSet):
    queryset = Tag.objects.order_by("id")
    serializer_class = TagSerializer
    permission_classes = (IsAuthenticated, AdminOrReadonly)


class TaskViewSet(viewsets.ModelViewSet):
    queryset = (
        Task.objects.prefetch_related("tag")
        .select_related("author")
        .select_related("executor")
        .order_by("id")
    )
    serializer_class = TaskSerializer
    filterset_class = TaskFilter
    permission_classes = (IsAuthenticated, AdminOrReadonly)


def generate_error(request: HttpRequest) -> None:
    a = None
    a.hello()


class CurrentUserViewSet(
    SingleResourceMixin, SingleResourceUpdateMixin, viewsets.ModelViewSet
):
    serializer_class = UserSerializer
    queryset = User.objects.order_by("id")
    permission_classes = (IsAuthenticated, AdminOrReadonly)

    def get_object(self) -> User:
        return cast(User, self.request.user)


class UserTasksViewSet(NestedViewSetMixin, viewsets.ReadOnlyModelViewSet):
    queryset = (
        Task.objects.prefetch_related("tag")
        .select_related("author")
        .select_related("executor")
        .order_by("id")
    )
    serializer_class = TaskSerializer
    filterset_class = TaskFilter
    permission_classes = (IsAuthenticated, AdminOrReadonly)


class TaskTagsViewSet(viewsets.ModelViewSet):
    serializer_class = TagSerializer
    permission_classes = (IsAuthenticated, AdminOrReadonly)

    def get_queryset(self) -> QuerySet:
        task_id = self.kwargs["parent_lookup_task_id"]
        return Task.objects.get(pk=task_id).tag.all()

    def perform_create(self, serializer: BaseSerializer) -> None:
        tag_obj = serializer.save()
        task_id = self.kwargs["parent_lookup_task_id"]
        Task.objects.get(pk=task_id).tag.add(tag_obj)


class CountdownJobViewSet(mixins.CreateModelMixin, viewsets.GenericViewSet):
    serializer_class = CountdownJobSerializer

    def get_success_headers(self, data: dict) -> dict[str, str]:
        task_id = data["task_id"]
        return {"Location": reverse("jobs-detail", args=[task_id])}


class AsyncJobViewSet(viewsets.GenericViewSet):
    serializer_class = JobSerializer

    def get_object(self) -> AsyncJob:
        lookup_url_kwargs = self.lookup_url_kwarg or self.lookup_field
        task_id = self.kwargs[lookup_url_kwargs]
        job = AsyncJob.from_id(task_id)
        if job.status == JobStatus.UNKNOWN:
            raise Http404()
        return job

    def retrieve(self, request: Request, *args: Any, **kwargs: Any) -> HttpResponse:
        instance = self.get_object()
        serializer_data = self.get_serializer(instance).data
        if instance.status == JobStatus.SUCCESS:
            location = self.request.build_absolute_uri(instance.result)
            return Response(
                serializer_data,
                headers={"location": location},
                status=status.HTTP_201_CREATED,
            )
        return Response(serializer_data)
