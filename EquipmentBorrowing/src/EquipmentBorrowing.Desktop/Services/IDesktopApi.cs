namespace EquipmentBorrowing.Desktop.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

public interface IDesktopApi
{
    Task<QueryResult<Equipment>> GetAllEquipmentAsync();
    Task<QueryResult<Equipment>> GetAvailableEquipmentAsync();
    Task<QueryResult<Equipment>> SearchEquipmentAsync(string? searchText);
    Task<QueryResult<Student>> GetAllStudentsAsync();
    Task<QueryResult<Student>> SearchStudentsAsync(string? searchText);
    Task<QueryResult<BorrowingRecord>> GetActiveBorrowingsAsync();
    Task<QueryResult<BorrowingRecord>> SearchBorrowingsAsync(BorrowingSearchCriteria criteria);
    Task<QueryResult<BorrowingRecord>> GetStudentHistoryAsync(int studentId);
    Task<QueryResult<BorrowingRecord>> GetEquipmentHistoryAsync(int equipmentId);
    Task<OperationResult> BorrowAsync(int studentId, int equipmentId, DateOnly? expectedReturnDate);
    Task<OperationResult> CheckOutMultipleAsync(int studentId, List<int> equipmentIds, DateOnly? expectedReturnDate);
    Task<OperationResult> ReturnAsync(int borrowingId);
    Task<OperationResult> RegisterStudentAsync(string? studentNumber, string? fullName, string? contactInfo);
    Task<OperationResult> UpdateStudentAsync(int studentId, string? studentNumber, string? fullName, string? contactInfo);
    Task<OperationResult> ChangeEligibilityAsync(int studentId, bool allowToBorrow);
    Task<OperationResult> RegisterEquipmentAsync(string? equipmentCode, string? name, string? description);
    Task<OperationResult> UpdateEquipmentAsync(int equipmentId, string? equipmentCode, string? name, string? description);
}
