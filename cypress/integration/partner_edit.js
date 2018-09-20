describe('find and edit partner', function() {
  it('logs in with user demo and modifies partner', function() {
    cy.visit('/Partner/Partners/Maintain/MaintainPartners')
    cy.get('#txtEmail').should("be.visible")
    cy.get('#txtEmail').type('demo')
    cy.get('#txtPassword').type('demo')
    cy.get('#btnLogin').click()
    cy.get('#logout').should("be.visible")

    cy.get('#btnFilter').click()
    cy.get('input[name="AFamilyNameOrOrganisation"]').type('jacob')
    cy.get('#btnSearch').click()
    cy.get('#partner43012945').should("be.visible").click()
    cy.get('#partner43012945 #btnEdit').should("be.visible").click()
    cy.get('#modal_space input[name="PFamily_p_first_name_c"]').should('have.value', "Holger")
    cy.get('#modal_space input[name="PFamily_p_first_name_c"]').clear().type('Albrecht')
    cy.get('#modal_space #btnSave').click()
    cy.get('#message').should("be.visible").should("contain", 'Successfully saved')

    cy.visit('/Partner/Partners/Maintain/MaintainPartners')
    cy.get('#btnFilter').click()
    cy.get('input[name="AFamilyNameOrOrganisation"]').type('jacob')
    cy.get('#btnSearch').click()
    cy.get('#partner43012945').should("be.visible").click()
    cy.get('#partner43012945 #btnEdit').should("be.visible").click()
    cy.get('#modal_space input[name="PFamily_p_first_name_c"]').should('have.value', "Albrecht")
    cy.get('#modal_space input[name="PFamily_p_first_name_c"]').clear().type('Holger')
    cy.get('#modal_space #btnSave').click()
    cy.get('#message').should("be.visible")
    cy.get('#message').should("contain", 'Successfully saved')

    cy.get('#logout').click();
  })
})
