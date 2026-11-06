from django.db import models


class Tag(models.Model):
    title = models.CharField(max_length=100)
    unique_id = models.BigIntegerField(unique=True, blank=True, null=True)
