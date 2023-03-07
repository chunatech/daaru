(*
    Goals of this module: 
    - Compose a Transaction from a .cwt into a canopy friendly 
      format to be executed with canopy, preserving the end users 
      settings and package additions



    [discussion needed]
    - how the pipeline up until a transaction is sent to the composer will look
    - methods for receiving new transactions (sub/pub, event hooks, etc)
    - validation parameters of a cwt pre processing? 
    - how will the composer recieve items to queue and process? 
        - ideas for this: 
            - as a tuple (TransactionSettings, FilePath) or (TransactionSettings, FileContent)
                - if verification of permissions / authority ends up requiring 
                  the content to be opened already, maybe its better to just 
                  send the content instead of the filepath to reduce IO calls 

    ================================================================================
    Implementation

    inputs needed: 
    - .cwt contents/filepath
    - configuration items related to transaction (TransactionSettings)
    
    output expected: 
    - for each .cwt processed 
        - an .fsx file that represents a single transaction, which can be 
          run by the standalone fsi included in this package
    
    datatypes needed: 
    - queue to process the .cwts 

    methods needed:
    - custom enqueue for the processing queue 
        - run QueuedForProcess
    - validateTransactionSettings TransactionSettings
        - ensures the selenium settings passed from the user match known good settings
        - if the settings are not good, log and compose a new TransactionSettings record 
          with bad settings ommited to use in the remainder of the process. 
    - validate (TransactionSettings * CWTFilePathORContent)
        - runs validateTransactionSettings
        - needs to validate that the format is correct for a cwt file such that 
          it can be composed
        - runs CWTValidatedEvent
        - can |> into composeTransaction
    - composeHeaders (TransactionSettings * ValidatedContent)
        - stitch together necessary header information such as 
          needed #r imports, browser options to run the file with, and browser driver location,
          metadata from transaction composer
        - use inside of composeTransaction
    - composeFooters 
        - stitch together the necessary footer information 
            - canopy specific run method
        - use inside of composeTransaction
    - composeTransaction (validatedtransaction: (TransactionSettings * FileContent))
        - handles the transaction body composition post validation
        - run TransactionComposed 
        - should return the fsx content and can |> into writeFsx method
    - writeFsx 
        - write this such that composeTransaction can |> into this method 
        - should take the validated transaction from compose transaction and write it to an fsx 
          file  
        - run FSXWrittenEvent
    - generateReport 
        - create a report that details the success failure of a transaction for logging purposes
        - probably json format
        - run CompositionReportGenerated event
    - process (hook in right after enqueue of cwt queue ^)
        - handle the processing of a cwt from start to finish using the described methods and pipeline
        - run ProcessedEvent

    events [discussion needed]
    - QueuedForProcess cwt
    - CWTValidated cwt
    - TransactionComposed transaction 
    - FSXWritten filepath 
    - CompositionReportGenerated report
    - Processed msg

    errors needed: 
    - ValidationError
    - CompositionError
    - ReportingError

    operations order: 
    recieve transaction 
    queue transaction 
    process 
        validate |> composeTransaction |> writeFsx |> generateReport
        run ProcessedEvent  
*)