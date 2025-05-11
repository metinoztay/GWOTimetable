using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GWOTimetable.Models;

public partial class Db12026Context : DbContext
{
    public Db12026Context()
    {
    }

    public Db12026Context(DbContextOptions<Db12026Context> options)
        : base(options)
    {
    }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassConstraint> ClassConstraints { get; set; }

    public virtual DbSet<ClassCourse> ClassCourses { get; set; }

    public virtual DbSet<Classroom> Classrooms { get; set; }

    public virtual DbSet<ClassroomConstraint> ClassroomConstraints { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<Day> Days { get; set; }

    public virtual DbSet<Educator> Educators { get; set; }

    public virtual DbSet<EducatorConstraint> EducatorConstraints { get; set; }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Timetable> Timetables { get; set; }

    public virtual DbSet<TimetableConstraint> TimetableConstraints { get; set; }

    public virtual DbSet<TimetablePlacement> TimetablePlacements { get; set; }

    public virtual DbSet<TimetableState> TimetableStates { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VerificationCode> VerificationCodes { get; set; }

    public virtual DbSet<Workspace> Workspaces { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=db12026.public.databaseasp.net; Database=db12026; User Id=db12026; Password=qQ_53=pEJa4#; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;Connection Timeout=30");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId).HasName("PK__Classes");

            entity.Property(e => e.ClassName).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Workspace).WithMany(p => p.Classes)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Classes_WorkspaceId");
        });

        modelBuilder.Entity<ClassConstraint>(entity =>
        {
            entity.HasKey(e => e.ClassConstraintId).HasName("PK__ClassConstraints");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassConstraints)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassConstraints_ClassId");

            entity.HasOne(d => d.Day).WithMany(p => p.ClassConstraints)
                .HasForeignKey(d => d.DayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassConstraints_DayId");

            entity.HasOne(d => d.Lesson).WithMany(p => p.ClassConstraints)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassConstraints_LessonId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.ClassConstraints)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassConstraints_WorkspaceId");
        });

        modelBuilder.Entity<ClassCourse>(entity =>
        {
            entity.HasKey(e => e.ClassCourseId).HasName("PK__ClassCourses");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassCourses)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassCourses_ClassId");

            entity.HasOne(d => d.Classroom).WithMany(p => p.ClassCourses)
                .HasForeignKey(d => d.ClassroomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassCourses_ClassRoomId");

            entity.HasOne(d => d.Course).WithMany(p => p.ClassCourses)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassCourses_CourseId");

            entity.HasOne(d => d.Educator).WithMany(p => p.ClassCourses)
                .HasForeignKey(d => d.EducatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassCourses_EducatorId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.ClassCourses)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassCourses_WorkspaceId");
        });

        modelBuilder.Entity<Classroom>(entity =>
        {
            entity.HasKey(e => e.ClassroomId).HasName("PK__ClassRooms");

            entity.Property(e => e.ClassroomName).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Workspace).WithMany(p => p.Classrooms)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassRooms_WorkspaceId");
        });

        modelBuilder.Entity<ClassroomConstraint>(entity =>
        {
            entity.HasKey(e => e.ClassroomConstraintId).HasName("PK__ClassRoomConstraints");

            entity.HasOne(d => d.Classroom).WithMany(p => p.ClassroomConstraints)
                .HasForeignKey(d => d.ClassroomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassroomConstraints_ClassroomId");

            entity.HasOne(d => d.Day).WithMany(p => p.ClassroomConstraints)
                .HasForeignKey(d => d.DayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassroomConstraints_DayId");

            entity.HasOne(d => d.Lesson).WithMany(p => p.ClassroomConstraints)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassroomConstraints_LessonId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.ClassroomConstraints)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClassroomConstraints_WorkspaceId");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__Courses");

            entity.Property(e => e.CourseCode).HasMaxLength(15);
            entity.Property(e => e.CourseName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.PlacementFormat)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Workspace).WithMany(p => p.Courses)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Courses_WorkspaceId");
        });

        modelBuilder.Entity<Day>(entity =>
        {
            entity.HasKey(e => e.DayId).HasName("PK__Days");

            entity.Property(e => e.DayOfWeek).HasMaxLength(20);
            entity.Property(e => e.ShortName).HasMaxLength(5);

            entity.HasOne(d => d.Workspace).WithMany(p => p.Days)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Days_WorkspaceId");
        });

        modelBuilder.Entity<Educator>(entity =>
        {
            entity.HasKey(e => e.EducatorId).HasName("PK__Educators");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.ShortName).HasMaxLength(10);
            entity.Property(e => e.Title).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Workspace).WithMany(p => p.Educators)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Educators_WorkspaceId");
        });

        modelBuilder.Entity<EducatorConstraint>(entity =>
        {
            entity.HasKey(e => e.EducatorConstraints).HasName("PK__EducatorConstraints");

            entity.HasOne(d => d.Day).WithMany(p => p.EducatorConstraints)
                .HasForeignKey(d => d.DayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EducatorConstraints_DayId");

            entity.HasOne(d => d.Educator).WithMany(p => p.EducatorConstraints)
                .HasForeignKey(d => d.EducatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EducatorConstraints_EducatorId");

            entity.HasOne(d => d.Lesson).WithMany(p => p.EducatorConstraints)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EducatorConstraints_LessonId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.EducatorConstraints)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EducatorConstraints_WorkspaceId");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.LessonId).HasName("PK__Lessons");

            entity.Property(e => e.EndTime)
                .HasMaxLength(5)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.StartTime)
                .HasMaxLength(5)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.Workspace).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lessons_WorkspaceId");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.RoleName).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Timetable>(entity =>
        {
            entity.HasKey(e => e.TimetableId).HasName("PK__Timetables");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.Tag).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.TimetableState).WithMany(p => p.Timetables)
                .HasForeignKey(d => d.TimetableStateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Timetables_TimetableStatesId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.Timetables)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Timetables_WorkspaceId");
        });

        modelBuilder.Entity<TimetableConstraint>(entity =>
        {
            entity.HasKey(e => e.TimetableConstraintId).HasName("PK__TimetableConstraints");

            entity.HasOne(d => d.ClassCourse).WithMany(p => p.TimetableConstraints)
                .HasForeignKey(d => d.ClassCourseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableConstraints_ClassCourseId");

            entity.HasOne(d => d.Day).WithMany(p => p.TimetableConstraints)
                .HasForeignKey(d => d.DayId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableConstraints_DayId");

            entity.HasOne(d => d.Lesson).WithMany(p => p.TimetableConstraints)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableConstraints_LessonId");

            entity.HasOne(d => d.Workspace).WithMany(p => p.TimetableConstraints)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetableConstraints_WorkspaceId");
        });

        modelBuilder.Entity<TimetablePlacement>(entity =>
        {
            entity.HasKey(e => e.TimetablePlacementId).HasName("PK__TimetablePlacements");

            entity.Property(e => e.ClassName).HasMaxLength(50);
            entity.Property(e => e.ClassroomName).HasMaxLength(50);
            entity.Property(e => e.CourseCode).HasMaxLength(15);
            entity.Property(e => e.CourseName).HasMaxLength(100);
            entity.Property(e => e.DayOfWeek).HasMaxLength(20);
            entity.Property(e => e.DayShortName).HasMaxLength(5);
            entity.Property(e => e.EducatorFullName).HasMaxLength(120);
            entity.Property(e => e.EducatorShortName).HasMaxLength(10);
            entity.Property(e => e.EndTime)
                .HasMaxLength(5)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.StartTime)
                .HasMaxLength(5)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.Workspace).WithMany(p => p.TimetablePlacements)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimetablePlacements_WorkspaceId");
        });

        modelBuilder.Entity<TimetableState>(entity =>
        {
            entity.HasKey(e => e.TimetableStateId).HasName("PK__TimetableStates");

            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.State).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__UserId");

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.PhotoUrl).HasMaxLength(250);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_RoleId");
        });

        modelBuilder.Entity<VerificationCode>(entity =>
        {
            entity.HasKey(e => e.VerificationCodeId).HasName("PK__VerificationCodes");

            entity.Property(e => e.CodeHash)
                .HasMaxLength(32)
                .IsFixedLength();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.ExpirationAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany(p => p.VerificationCodes)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VerificationCodes_UserId");
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.WorkspaceId).HasName("PK__Workspaces");

            entity.Property(e => e.WorkspaceId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.WorkspaceName).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.Workspaces)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Workspaces_UserId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
