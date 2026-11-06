from django.db import models
from .user import User
from .tag import Tag


class Task(models.Model):
    class Conditions(models.TextChoices):
        NEW_TASK = "new_task"
        IN_DEV = "in_development"
        IN_QA = "in_qa"
        IN_CR = "in_code_review"
        READY_F_REALEASE = "ready_for_release"
        REALEASED = "released"
        ARCHIVED = "archived"

    title = models.CharField(max_length=100)
    description = models.TextField(blank=True, null=True)
    date_created = models.DateField(auto_now_add=True)
    date_updated = models.DateField(auto_now=True)
    date_to_finish = models.DateField(blank=True, null=True)
    condition = models.CharField(
        max_length=50, default=Conditions.NEW_TASK, choices=Conditions.choices
    )
    priority = models.PositiveSmallIntegerField(blank=True, null=True)
    author = models.ForeignKey(
        User, on_delete=models.SET_NULL, related_name="tasks_author", null=True
    )
    executor = models.ForeignKey(
        User, on_delete=models.SET_NULL, related_name="tasks_executor", null=True
    )
    tag = models.ManyToManyField(Tag, blank=True)
