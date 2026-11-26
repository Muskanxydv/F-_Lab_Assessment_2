// For more information see https://aka.ms/fsharp-console-apps

//Build an integrated F# application covering Sessions 6–8. Requirements:
//(1) model a small domain (e.g., library records or student management) using F# types, records, and/or classes;
//(2) process domain data using higher-order functions (map, filter) and pattern matching;
//(3) persist records to a relational or NoSQL database with CRUD operations, using async workflows where appropriate.



open System
open Microsoft.Data.Sqlite

type Bookcategory =
    | Fiction
    | NonFiction
    | Biography
    | Literature
    | Science

type Book = {
    Id: int
    Title: string
    Author: string
    Category: Bookcategory
    IsAvailable: bool
}
let stringToCategory (category: string) =
    match category.ToLower() with
    | "fiction" -> Some Fiction
    | "nonfiction" -> Some NonFiction
    | "science" -> Some Science
    | "literature" -> Some Literature
    | "biography" -> Some Biography
    | _ -> None

let categoryToString category =
    match category with
    | Fiction -> "Fiction"
    | NonFiction -> "NonFiction"
    | Science -> "Science"
    | Literature -> "Literature"
    | Biography -> "Biography"


let connectionString = "Data Source=library.db"

let initializeDatabase () =
    async {
        use connection = new SqliteConnection(connectionString)
        do! connection.OpenAsync() |> Async.AwaitTask

        use command = connection.CreateCommand()

        command.CommandText <-
            "CREATE TABLE IF NOT EXISTS Books (Id INTEGER PRIMARY KEY, Title TEXT, Author TEXT, Category TEXT, IsAvailable INTEGER)"

        let! _ = command.ExecuteNonQueryAsync() |> Async.AwaitTask
        return ()
    }

let addBook (book: Book) =
    async {
        use connection = new SqliteConnection(connectionString)
        do! connection.OpenAsync() |> Async.AwaitTask

        use insert = connection.CreateCommand()

        insert.CommandText <-
            "INSERT INTO Books (Id, Title, Author, Category, IsAvailable) VALUES (@Id, @Title, @Author, @Category, @IsAvailable)"

        insert.Parameters.AddWithValue("@Id", book.Id) |> ignore
        insert.Parameters.AddWithValue("@Title", book.Title) |> ignore
        insert.Parameters.AddWithValue("@Author", book.Author) |> ignore
        insert.Parameters.AddWithValue("@Category", categoryToString book.Category) |> ignore
        insert.Parameters.AddWithValue("@IsAvailable", if book.IsAvailable then 1 else 0) |> ignore

        let! result = insert.ExecuteNonQueryAsync() |> Async.AwaitTask
        printfn "%d book added successfully." result
    }

let getBooks () =
    async {
        use connection = new SqliteConnection(connectionString)
        do! connection.OpenAsync() |> Async.AwaitTask

        use command = connection.CreateCommand()
        command.CommandText <- "SELECT Id, Title, Author, Category, IsAvailable FROM Books"

        use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask

        let books = ResizeArray<Book>()
        let mutable reading = true

        while reading do
            let! hasRow = reader.ReadAsync() |> Async.AwaitTask

            if hasRow then
                let category = stringToCategory (reader.GetString(3))

                match category with
                | Some c ->
                    books.Add({
                        Id = reader.GetInt32(0)
                        Title = reader.GetString(1)
                        Author = reader.GetString(2)
                        Category = c
                        IsAvailable = reader.GetInt32(4) = 1
                    })
                | None -> ()
            else
                reading <- false

        return books |> Seq.toList
    }

let searchBooksByCategory category =
    async {
        let! books = getBooks()
        return books |> List.filter (fun book -> book.Category = category)
    }

let displayBooks books =
    printfn "\nBook Records:"

    books
    |> List.iter (fun b ->
        let status =
            if b.IsAvailable then "Available"
            else "Issued"

        printfn "ID: %d | Title: %s | Author: %s | Category: %s | Status: %s"
            b.Id b.Title b.Author (categoryToString b.Category) status
    )

let readInt message =
    printf "%s" message
    match Int32.TryParse(Console.ReadLine()) with
    | true, value -> Some value
    | _ -> None

let readString message =
    printf "%s" message
    let value = Console.ReadLine()

    if String.IsNullOrWhiteSpace(value) then None
    else Some value

let readBook id =
    match readString "Enter book title: ",
          readString "Enter author: ",
          readString "Enter category: ",
          readString "Is book available? (yes/no): " with

    | Some title, Some author, Some category, Some availability ->
        match stringToCategory category, availability.ToLower() with
        | Some c, "yes" ->
            Some {
                Id = id
                Title = title
                Author = author
                Category = c
                IsAvailable = true
            }

        | Some c, "no" ->
            Some {
                Id = id
                Title = title
                Author = author
                Category = c
                IsAvailable = false
            }

        | _ ->
            printfn "Invalid category or availability."
            None

    | _ -> None

let rec menu () =
    printfn "\nLibrary Management System"
    printfn "1. Add Book"
    printfn "2. View All Books"
    printfn "3. Search Book by Category"
    printfn "4. Exit"

    match readInt "Enter your choice: " with

    | Some 1 ->
        match readInt "Enter Book ID: " with
        | Some id ->
            match readBook id with
            | Some book ->
                try
                    Async.RunSynchronously(addBook book)
                with ex ->
                    printfn "Error: %s" ex.Message
            | None -> ()
        | None -> printfn "Invalid ID."

        menu()

    | Some 2 ->
        try
            let books = Async.RunSynchronously(getBooks())
            displayBooks books
        with ex ->
            printfn "Error: %s" ex.Message

        menu()

    | Some 3 ->
        match readString "Enter book category: " with
        | Some category ->
            match stringToCategory category with
            | Some c ->
                try
                    let books = Async.RunSynchronously(searchBooksByCategory c)
                    displayBooks books
                with ex ->
                    printfn "Error: %s" ex.Message
            | None -> printfn "Invalid category."
        | None -> printfn "Category cannot be empty."

        menu()

    | Some 4 ->
        printfn "Thank you for using Library Management System."

    | _ ->
        printfn "Invalid choice."
        menu()

[<EntryPoint>]
let main argv =
    try
        Async.RunSynchronously(initializeDatabase())
        printfn "Database initialized successfully."
        menu()
        0
    with ex ->
        printfn "Error: %s" ex.Message
        1
