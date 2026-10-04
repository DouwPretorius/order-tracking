# Overview of build decisions
## Data Structure
I decided that with the structure of the orders and customers that a Postgresql databasxe would be a good quick solution that can then leverage off of the structured data system to keep those relations all structured well and be able to buidl some async calls when making additions and edits so that error processing could be processed as well.
I also made some decisions on things like the SKU being an optional but unique field if present, as sometimes from experience there is not always a value to be used for this but we also don't want items in the same order that do have them present to be doubled up on.
I also made teh decision that the unique thing of each Customer was their email, needing that to be unique for each as that is a pretty standard practice and their might be a shared phone number or multiple people with the same name across multiple organisations.
This also let me build some basic migrations to make sure that the solution can be run as needed and if I wanted to that I could then containerise the solution to have a basic setup that could be run where I needed

## Validation
### Front End
The front end was quite secondary to the solution due to the nature of most of the supplied business rules being required to be processed on the back end, but that did not mean that it was not useful to add in some extra checks for when deleting an order or customer. I made the decision to add in a state for when stock had been procured bt not confirmed to be sent out and that with the basic flow meant that I made a couple of decisions on which options could be used from the front end. I also tried to make the front end have a bit more meaningful error messages.

### Back End
Many of the validations for the additions of items were made on the back end and informed by the rules provided. This could by and large be enforced by picking the right data types on the database that is recording the values but it was good to have these in place on the API calls so that if someone were to try access it they could get good info. A consideration would be to enable swagger for these end points in a future update.

### Time Out
The other big thing that needed to be handled here though is the time out of adding duplicate entries. I made sure that this is a configurable time limit from the start and worked with the suggested solution to create a unique string for the entries based on the contents of the item being added and then comparing if that string matches an item and if that item's created is within the time out to block new entries.

This allows for slightly quicker responses from the check as we don't need to check through all entries but just query if there is a duplicate of that unique string, and only if we find it then do the comparison of the created time. There was some thought also into making sure that an order that has the same line items but in a different order was still flagged as being a duplicate as well.

## Flow
I made the decision that all newly created orders need to be started in the `Pending` state, to be picked up for processing, looking to see if those items are available or not. At any stage an order can be moved to `Cancelled` but otherwise there is a one direction flow to the eventual `Completed` state. This felt liek a good balance for an initial run, though I would expect there to be needs for more states when customers make requests during processing or if there would need to be items added on to the orders as well later in the process. In which case the sditing would need to go back to the processing states, but as a first go of things I don't think that consideration was within the bounds of the solution.

## Tests
There are a number of tests that were generated for the processes. There are intergartion tests to make sure the connection to the database is in place. Functional tests to make sure that the logic of the system is in place without needing the database to make teh additions, edits, and removals. I also have some front end tests in place as well to make sure that there is some kindof regression testing in place there for when updates are made to not break the flow

# Future changes
Having some pagination on the table showing the orders would be useful as this gets larger, and some more filtering options beyond just the search as well would do well
Adding in a user management system instead of this shared workspace would be useful for tracking what is being processed by who, and can lead to some better audit data
There could be race conditions if this was all connected up and running on multiple systems, though the unique code for orders will help some of that if the same entry is made from two locations
More meta data for items and a full collection of what stock will help as well to allow for choices and not free text being used for the various order details
Some greater security is probably not the worst plan for the API to prevent injections and misuse of it from users not in the eventual user list
The reporting of the statuses of orders on the main page can be moved to it's own space and expended with the suggestions above to get some more detailed tracking and be able to drill down. The most recent update messaging can also be moved out into a toast notification or the like as well.
The process which detects duplicate entries could also do with some refining as while we check the hole order and don't care for the order of sub items we might have want to allow different quantities and sku and pricing to all trigger the prevention as well. There would need to be some investigation as to what might be best there