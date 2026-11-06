from django.contrib import admin
from django.contrib.auth.admin import UserAdmin
from .models import User, Tag, Task


class TaskManagerAdminSite(admin.AdminSite):
    pass


task_manager_admin_site = TaskManagerAdminSite(name="Task manager admin")


@admin.register(Tag, site=task_manager_admin_site)
class TagAdmin(admin.ModelAdmin):
    list_display = ["title", "unique_id"]


@admin.register(Task, site=task_manager_admin_site)
class TaskAdmin(admin.ModelAdmin):
    list_display = [
        "title",
        "condition",
        "date_created",
        "date_updated",
        "author",
        "executor",
    ]


class UserAdmin(admin.ModelAdmin):
    list_display = ["username", "email", "first_name", "last_name", "role", "is_staff"]


task_manager_admin_site.register(User, UserAdmin)
