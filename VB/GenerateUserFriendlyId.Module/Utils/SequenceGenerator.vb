Imports Microsoft.VisualBasic
Imports System
Imports DevExpress.Xpo
Imports System.Threading
Imports System.ComponentModel
Imports DevExpress.ExpressApp
Imports DevExpress.Xpo.Metadata
Imports DevExpress.ExpressApp.DC
Imports System.Collections.Generic
Imports DevExpress.Xpo.DB.Exceptions

Namespace GenerateUserFriendlyId.Module.Utils
	'This class is used to generate sequential numbers for persistent objects.
	'Use the GetNextSequence method to get the next number and the Accept method, to save these changes to the database.
	Public Class SequenceGenerator
		Implements IDisposable
		Public Const MaxGenerationAttemptsCount As Integer = 10
		Public Const MinGenerationAttemptsDelay As Integer = 100
		Private Shared defaultDataLayer_Renamed As IDataLayer
		Private euow As ExplicitUnitOfWork
		Private seq As Sequence
		Public Sub New()
			Dim count As Integer = MaxGenerationAttemptsCount
			Do
				Try
					euow = New ExplicitUnitOfWork(DefaultDataLayer)
					Dim sequences As New XPCollection(Of Sequence)(euow)
					For Each seq As Sequence In sequences
						seq.Save()
					Next seq
					euow.FlushChanges()
					Exit Do
				Catch e1 As LockingException
					Close()
					count -= 1
					If count <= 0 Then
						Throw
					End If
					Thread.Sleep(MinGenerationAttemptsDelay * count)
				End Try
			Loop
		End Sub
		Public Sub Accept()
			euow.CommitChanges()
		End Sub
		Public Function GetNextSequence(ByVal theObject As Object) As Long
			If theObject Is Nothing Then
				Throw New ArgumentNullException("theObject")
			End If
			Return GetNextSequence(XafTypesInfo.Instance.FindTypeInfo(theObject.GetType()))
		End Function
		Public Function GetNextSequence(ByVal typeInfo As ITypeInfo) As Long
			If typeInfo Is Nothing Then
				Throw New ArgumentNullException("typeInfo")
			End If
			Return GetNextSequence(XafTypesInfo.XpoTypeInfoSource.XPDictionary.GetClassInfo(typeInfo.Type))
		End Function
		Public Function GetNextSequence(ByVal classInfo As XPClassInfo) As Long
			If classInfo Is Nothing Then
				Throw New ArgumentNullException("classInfo")
			End If
			seq = euow.GetObjectByKey(Of Sequence)(classInfo.FullName, True)
			If seq Is Nothing Then
				Throw New InvalidOperationException(String.Format("Sequence for the {0} type was not found.", classInfo.FullName))
			End If
			Dim nextSequence As Long = seq.NextSequence
			seq.NextSequence += 1
			euow.FlushChanges()
			Return nextSequence
		End Function
		Public Sub Close()
			If euow IsNot Nothing Then
				euow.Dispose()
				euow = Nothing
			End If
		End Sub
		Public Sub Dispose() Implements IDisposable.Dispose
			Close()
		End Sub
		Public Shared Property DefaultDataLayer() As IDataLayer
			Get
				If defaultDataLayer_Renamed Is Nothing Then
					Throw New ArgumentNullException("DefaultDataLayer")
				End If
				Return defaultDataLayer_Renamed
			End Get
			Set(ByVal value As IDataLayer)
				defaultDataLayer_Renamed = value
			End Set
		End Property
		Public Shared Sub RegisterSequences(ByVal persistentTypes As IEnumerable(Of ITypeInfo))
			If persistentTypes IsNot Nothing Then
				Using uow As New UnitOfWork(DefaultDataLayer)
					Dim sequenceList As New XPCollection(Of Sequence)(uow)
					Dim typeToExistsMap As New Dictionary(Of String, Boolean)()
					For Each seq As Sequence In sequenceList
						typeToExistsMap(seq.TypeName) = True
					Next seq
					For Each typeInfo As ITypeInfo In persistentTypes
						If typeToExistsMap.ContainsKey(typeInfo.FullName) Then
							Continue For
						End If
						Dim seq As New Sequence(uow)
						seq.TypeName = typeInfo.FullName
						seq.NextSequence = 0
					Next typeInfo
					uow.CommitChanges()
				End Using
			End If
		End Sub
		Public Shared Sub RegisterSequences(ByVal persistentClasses As IEnumerable(Of XPClassInfo))
			If persistentClasses IsNot Nothing Then
				Using uow As New UnitOfWork(DefaultDataLayer)
					Dim sequenceList As New XPCollection(Of Sequence)(uow)
					Dim typeToExistsMap As New Dictionary(Of String, Boolean)()
					For Each seq As Sequence In sequenceList
						typeToExistsMap(seq.TypeName) = True
					Next seq
					For Each classInfo As XPClassInfo In persistentClasses
						If typeToExistsMap.ContainsKey(classInfo.FullName) Then
							Continue For
						End If
						Dim seq As New Sequence(uow)
						seq.TypeName = classInfo.FullName
						seq.NextSequence = 0
					Next classInfo
					uow.CommitChanges()
				End Using
			End If
		End Sub
	End Class
	'This persistent class is used to store last sequential number for persistent objects.
	Public Class Sequence
		Inherits XPBaseObject
		Private typeName_Renamed As String
		Private nextSequence_Renamed As Long
		Public Sub New(ByVal session As Session)
			MyBase.New(session)
		End Sub
		'Dennis: The size should be enough to store a full type name. However, you cannot use unlimited size for key columns.
		<Key, Size(1024)> _
		Public Property TypeName() As String
			Get
				Return typeName_Renamed
			End Get
			Set(ByVal value As String)
				SetPropertyValue("TypeName", typeName_Renamed, value)
			End Set
		End Property
		Public Property NextSequence() As Long
			Get
				Return nextSequence_Renamed
			End Get
			Set(ByVal value As Long)
				SetPropertyValue("NextSequence", nextSequence_Renamed, value)
			End Set
		End Property
	End Class
	Public Interface ISupportSequentialNumber
		Property SequentialNumber() As Long
	End Interface
End Namespace