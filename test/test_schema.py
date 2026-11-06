from main.views import AsyncJobViewSet, TaskTagsViewSet


def test_nested_tags_schema_without_task_id():
    view = TaskTagsViewSet()
    view.swagger_fake_view = True
    view.kwargs = {}

    assert list(view.get_queryset()) == []


def test_async_job_has_no_model_queryset():
    assert AsyncJobViewSet().get_queryset() is None
