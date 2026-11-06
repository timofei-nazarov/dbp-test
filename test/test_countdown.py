import time
import pathlib

import pytest
from django.test import override_settings
from rest_framework import status

from test.base import TestViewSetBase


class TestCountdownJob(TestViewSetBase):
    basename = "countdown"
    COUNTDOWN_TIME = 5

    @pytest.mark.slow
    def test_countdown_machinery(self):
        response = self.request_create({"seconds": self.COUNTDOWN_TIME})
        assert response.status_code == status.HTTP_201_CREATED

        job_location = response.headers["Location"]
        start = time.monotonic()
        while response.data.get("status") != "success":
            time_out = start + self.COUNTDOWN_TIME + 1
            assert time.monotonic() < time_out
            response = self.api_client.get(job_location)

        assert time.monotonic() > start + self.COUNTDOWN_TIME
        file_name = response.headers["Location"].split("/", 3)[-1]
        file = pathlib.Path(file_name)
        assert file.is_file()
        assert file.read_bytes() == b"test data"
        file.unlink(missing_ok=True)

    @override_settings(CELERY_TASK_ALWAYS_EAGER=True)
    def test_countdown(self):
        response = self.request_create({"seconds": 1})
        task_id = response.data["task_id"]
        file_name = f"media/test_report-{task_id}.data"
        file = pathlib.Path(file_name)

        assert file.is_file()
        assert file.read_bytes() == b"test data"
        file.unlink(missing_ok=True)
