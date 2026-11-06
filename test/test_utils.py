import pytest
from rest_framework.test import APITestCase

from task_manager.utils import strtobool


class TestUtilsError(APITestCase):
    false_value: str
    true_value: str
    error_value: str

    @classmethod
    def setUpTestData(cls) -> None:
        cls.false_value = "f"
        cls.true_value = "t"
        cls.error_value = "some_value"

    def test_false(self):
        result = strtobool(self.false_value)
        assert result == 0

    def test_true(self):
        result = strtobool(self.true_value)
        assert result == 1

    def test_value_error(self):
        with self.assertRaises(ValueError):
            strtobool(self.error_value)
