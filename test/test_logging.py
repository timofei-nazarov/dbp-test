import logging
from datetime import datetime, timedelta

from django.http import HttpRequest

from task_manager.log_utils import PlaceHolder, RequestFormatter, _thread_locals


def test_format_without_request_context(monkeypatch):
    monkeypatch.delattr(_thread_locals, "request", raising=False)
    record = logging.LogRecord(
        "django.server", logging.INFO, "", 0, "static request", (), None
    )
    formatter = RequestFormatter("{duration} {message}", style="{")

    assert formatter.format(record) == "- static request"


def test_duration_without_start_time():
    assert RequestFormatter.get_duration(HttpRequest()) == "-"
    assert RequestFormatter.get_duration(PlaceHolder()) == "-"


def test_duration_with_start_time():
    request = HttpRequest()
    request.start_time = datetime.now() - timedelta(seconds=2)

    assert RequestFormatter.get_duration(request) == "2 seconds"
